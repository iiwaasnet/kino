using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;

namespace kino.Connectivity
{
    public class ClusterMonitor : IClusterMonitor
    {
        private readonly ISocketFactory socketFactory;
        private CancellationTokenSource monitoringToken;
        private CancellationTokenSource messageProcessingToken;
        private readonly BlockingCollection<IMessage> outgoingMessages;
        private readonly IClusterConfiguration clusterConfiguration;
        private readonly RouterConfiguration routerConfiguration;
        private Task sendingMessages;
        private Task listenningMessages;
        private Task monitorRendezvous;
        private readonly ManualResetEventSlim pingReceived;
        private readonly ManualResetEventSlim newRendezvousLeaderSelected;
        private readonly IRendezvousConfiguration rendezvousConfiguration;
        private ISocket clusterMonitorSubscriptionSocket;
        private ISocket clusterMonitorSendingSocket;
        private readonly ClusterTimingConfiguration timingConfiguration;
        private readonly ILogger logger;

        public ClusterMonitor(ISocketFactory socketFactory,
                              RouterConfiguration routerConfiguration,
                              IClusterConfiguration clusterConfiguration,
                              ClusterTimingConfiguration timingConfiguration,
                              IRendezvousConfiguration rendezvousConfiguration,
                              ILogger logger)
        {
            this.logger = logger;
            this.timingConfiguration = timingConfiguration;
            pingReceived = new ManualResetEventSlim(false);
            newRendezvousLeaderSelected = new ManualResetEventSlim(false);
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
            this.clusterConfiguration = clusterConfiguration;
            this.rendezvousConfiguration = rendezvousConfiguration;
            outgoingMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
        }

        public void Start()
        {
            StartProcessingClusterMessages();
            StartRendezvousMonitoring();
        }

        public void Stop()
        {
            StopRendezvousMonitoring();
            StopProcessingClusterMessages();
        }

        private void StartRendezvousMonitoring()
        {
            monitoringToken = new CancellationTokenSource();
            monitorRendezvous = Task.Factory.StartNew(_ => RendezvousConnectionMonitor(monitoringToken.Token),
                                                      TaskCreationOptions.LongRunning);
        }

        private void StartProcessingClusterMessages()
        {
            messageProcessingToken = new CancellationTokenSource();
            const int participantCount = 3;
            using (var gateway = new Barrier(participantCount))
            {
                sendingMessages = Task.Factory.StartNew(_ => SendMessages(messageProcessingToken.Token, gateway),
                                                        TaskCreationOptions.LongRunning);
                listenningMessages = Task.Factory.StartNew(_ => ListenMessages(messageProcessingToken.Token, gateway),
                                                           TaskCreationOptions.LongRunning);
                gateway.SignalAndWait(messageProcessingToken.Token);
            }
        }

        private void StopProcessingClusterMessages()
        {
            messageProcessingToken.Cancel();
            sendingMessages.Wait();
            listenningMessages.Wait();
            messageProcessingToken.Dispose();
        }

        private void StopRendezvousMonitoring()
        {
            monitoringToken.Cancel();
            monitorRendezvous.Wait();
            monitoringToken.Dispose();
            pingReceived.Dispose();
            newRendezvousLeaderSelected.Dispose();
        }

        private void RendezvousConnectionMonitor(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (PingSilence())
                    {
                        StopProcessingClusterMessages();
                        StartProcessingClusterMessages();

                        var rendezvousServer = rendezvousConfiguration.GetCurrentRendezvousServer();
                        logger.Info($"Reconnected to Rendezvous {rendezvousServer.MulticastUri.AbsoluteUri}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private bool PingSilence()
        {
            const int NewLeaderElected = 1;
            var result = WaitHandle.WaitAny(new[] {pingReceived.WaitHandle, newRendezvousLeaderSelected.WaitHandle},
                                            timingConfiguration.PingSilenceBeforeRendezvousFailover);
            if (result == WaitHandle.WaitTimeout)
            {
                rendezvousConfiguration.RotateRendezvousServers();
                return true;
            }
            if (result == NewLeaderElected)
            {
                newRendezvousLeaderSelected.Reset();
                return true;
            }

            pingReceived.Reset();
            return false;
        }

        private void SendMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (clusterMonitorSendingSocket = CreateClusterMonitorSendingSocket())
                {
                    gateway.SignalAndWait(token);
                    try
                    {
                        foreach (var messageOut in outgoingMessages.GetConsumingEnumerable(token))
                        {
                            clusterMonitorSendingSocket.SendMessage(messageOut);
                            // TODO: Block immediatelly for the response
                            // Otherwise, consider the RS dead and switch to failover partner
                            //sendingSocket.ReceiveMessage(token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    clusterMonitorSendingSocket.SendMessage(CreateUnregisterRoutingMessage());
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void ListenMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (clusterMonitorSubscriptionSocket = CreateClusterMonitorSubscriptionSocket())
                {
                    using (var routerNotificationSocket = CreateRouterCommunicationSocket())
                    {
                        gateway.SignalAndWait(token);

                        while (!token.IsCancellationRequested)
                        {
                            var message = clusterMonitorSubscriptionSocket.ReceiveMessage(token);
                            if (message != null)
                            {
                                ProcessIncomingMessage(message, routerNotificationSocket);
                            }
                        }
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private ISocket CreateClusterMonitorSubscriptionSocket()
        {
            var rendezvousServer = rendezvousConfiguration.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Connect(rendezvousServer.MulticastUri);
            socket.Subscribe();

            logger.Info($"Connected to Rendezvous {rendezvousServer.MulticastUri.AbsoluteUri}");

            return socket;
        }

        private ISocket CreateClusterMonitorSendingSocket()
        {
            var rendezvousServer = rendezvousConfiguration.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(rendezvousServer.UnicastUri);

            return socket;
        }

        private ISocket CreateRouterCommunicationSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(routerConfiguration.RouterAddress.Uri);

            return socket;
        }

        private bool ProcessIncomingMessage(IMessage message, ISocket routerNotificationSocket)
            => RegisterExternalRoute(message, routerNotificationSocket)
               || Ping(message, routerNotificationSocket)
               || Pong(message)
               || RequestMessageHandlersRouting(message, routerNotificationSocket)
               || UnregisterRoute(message, routerNotificationSocket)
               || RendezvousNotLeader(message);

        private bool RendezvousNotLeader(IMessage message)
        {
            var shouldHandle = IsRendezvousNotLeader(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RendezvousNotLeaderMessage>();
                var newLeader = new RendezvousEndpoints
                                {
                                    MulticastUri = new Uri(payload.LeaderMulticastUri),
                                    UnicastUri = new Uri(payload.LeaderUnicastUri)
                                };
                if (!rendezvousConfiguration.GetCurrentRendezvousServer().Equals(newLeader))
                {
                    rendezvousConfiguration.SetCurrentRendezvousServer(newLeader);
                    newRendezvousLeaderSelected.Set();
                }
            }

            return shouldHandle;
        }

        private bool UnregisterRoute(IMessage message, ISocket routerNotificationSocket)
        {
            var shouldHandle = IsUnregisterMessageHandlersRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterMessageHandlersRoutingMessage>();
                clusterConfiguration.DeleteClusterMember(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
                routerNotificationSocket.SendMessage(message);
            }

            return shouldHandle;
        }

        private bool RegisterExternalRoute(IMessage message, ISocket routerNotificationSocket)
        {
            var shouldHandler = IsRegisterExternalRoute(message);
            if (shouldHandler)
            {
                AddClusterMember(message);
                routerNotificationSocket.SendMessage(message);
            }

            return shouldHandler;
        }

        private bool Ping(IMessage message, ISocket routerNotificationSocket)
        {
            var shouldHandle = IsPing(message);
            if (shouldHandle)
            {
                pingReceived.Set();

                var ping = message.GetPayload<PingMessage>();
                SendPong(ping.PingId);

                UnregisterDeadNodes(routerNotificationSocket, DateTime.UtcNow, ping.PingInterval);
            }

            return shouldHandle;
        }

        private bool Pong(IMessage message)
        {
            var shouldHandle = IsPong(message);
            if (shouldHandle)
            {
                ProcessPongMessage(message);
            }

            return shouldHandle;
        }

        private bool RequestMessageHandlersRouting(IMessage message, ISocket routerNotificationSocket)
        {
            var shouldHandle = IsRequestAllMessageHandlersRouting(message)
                               || IsRequestNodeMessageHandlersRouting(message);
            if (shouldHandle)
            {
                routerNotificationSocket.SendMessage(message);
            }

            return shouldHandle;
        }

        public void RegisterSelf(IEnumerable<MessageHandlerIdentifier> messageHandlers)
        {
            var message = Message.Create(new RegisterMessageHandlersRoutingMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             MessageHandlers = messageHandlers.Select(mh => new MessageHandlerRegistration
                                                                                            {
                                                                                                Version = mh.Version,
                                                                                                Identity = mh.Identity
                                                                                            }).ToArray()
                                         },
                                         RegisterMessageHandlersRoutingMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        public void RequestMessageHandlersRouting()
        {
            var message = Message.Create(new RequestAllMessageHandlersRoutingMessage
                                         {
                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                                         },
                                         RequestAllMessageHandlersRoutingMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        private IMessage CreateUnregisterRoutingMessage()
            => Message.Create(new UnregisterMessageHandlersRoutingMessage
                              {
                                  Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                  SocketIdentity = routerConfiguration.ScaleOutAddress.Identity
                              },
                              UnregisterMessageHandlersRoutingMessage.MessageIdentity);

        private bool IsRegisterExternalRoute(IMessage message)
        {
            if (Unsafe.Equals(RegisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<RegisterMessageHandlersRoutingMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsPing(IMessage message)
            => Unsafe.Equals(PingMessage.MessageIdentity, message.Identity);

        private bool ThisNodeSocket(byte[] socketIdentity)
            => Unsafe.Equals(routerConfiguration.ScaleOutAddress.Identity, socketIdentity);

        private bool IsPong(IMessage message)
        {
            if (Unsafe.Equals(PongMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<PongMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsUnregisterMessageHandlersRouting(IMessage message)
        {
            if (Unsafe.Equals(UnregisterMessageHandlersRoutingMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<UnregisterMessageHandlersRoutingMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsRendezvousNotLeader(IMessage message)
            => Unsafe.Equals(RendezvousNotLeaderMessage.MessageIdentity, message.Identity);

        private bool IsRequestAllMessageHandlersRouting(IMessage message)
        {
            if (Unsafe.Equals(RequestAllMessageHandlersRoutingMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<RequestAllMessageHandlersRoutingMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
            }

            return false;
        }

        private bool IsRequestNodeMessageHandlersRouting(IMessage message)
        {
            if (Unsafe.Equals(RequestNodeMessageHandlersRoutingMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<RequestNodeMessageHandlersRoutingMessage>();

                return ThisNodeSocket(payload.TargetNodeIdentity);
            }

            return false;
        }

        private void SendPong(ulong pingId)
        {
            var message = Message.Create(new PongMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             PingId = pingId
                                         },
                                         PongMessage.MessageIdentity);
            outgoingMessages.Add(message);
        }

        private void AddClusterMember(IMessage message)
        {
            var registration = message.GetPayload<RegisterMessageHandlersRoutingMessage>();
            var clusterMember = new SocketEndpoint(new Uri(registration.Uri), registration.SocketIdentity);
            clusterConfiguration.AddClusterMember(clusterMember);
        }

        private void ProcessPongMessage(IMessage message)
        {
            var payload = message.GetPayload<PongMessage>();

            var nodeNotFound = clusterConfiguration.KeepAlive(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
            if (!nodeNotFound)
            {
                RequestNodeMessageHandlersRouting(payload);
            }
        }

        private void RequestNodeMessageHandlersRouting(PongMessage payload)
        {
            var request = Message.Create(new RequestNodeMessageHandlersRoutingMessage
                                         {
                                             TargetNodeIdentity = payload.SocketIdentity,
                                             TargetNodeUri = payload.Uri
                                         },
                                         RequestNodeMessageHandlersRoutingMessage.MessageIdentity);
            outgoingMessages.Add(request);

            logger.Debug("Route not found. Requesting registrations for " +
                         $"Uri:{payload.Uri} " +
                         $"Socket:{payload.SocketIdentity.GetString()}");
        }

        private void UnregisterDeadNodes(ISocket routerNotificationSocket, DateTime pingTime, TimeSpan pingInterval)
        {
            foreach (var deadNode in clusterConfiguration.GetDeadMembers(pingTime, pingInterval))
            {
                var message = Message.Create(new UnregisterMessageHandlersRoutingMessage
                                             {
                                                 Uri = deadNode.Uri.ToSocketAddress(),
                                                 SocketIdentity = deadNode.Identity
                                             },
                                             UnregisterMessageHandlersRoutingMessage.MessageIdentity);
                clusterConfiguration.DeleteClusterMember(deadNode);
                routerNotificationSocket.SendMessage(message);
            }
        }
    }
}