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
    internal class ClusterMonitor : IClusterMonitor
    {
        private readonly ISocketFactory socketFactory;
        private CancellationTokenSource monitoringToken;
        private CancellationTokenSource messageProcessingToken;
        private readonly BlockingCollection<IMessage> outgoingMessages;
        private readonly IClusterMembership clusterMembership;
        private readonly RouterConfiguration routerConfiguration;
        private Task sendingMessages;
        private Task listenningMessages;
        private Task monitorRendezvous;
        private readonly ManualResetEventSlim pingReceived;
        private readonly ManualResetEventSlim newRendezvousConfiguration;
        private readonly IRendezvousCluster rendezvousCluster;
        private ISocket clusterMonitorSubscriptionSocket;
        private ISocket clusterMonitorSendingSocket;
        private readonly ClusterMembershipConfiguration membershipConfiguration;
        private readonly ILogger logger;

        public ClusterMonitor(ISocketFactory socketFactory,
                              RouterConfiguration routerConfiguration,
                              IClusterMembership clusterMembership,
                              ClusterMembershipConfiguration membershipConfiguration,
                              IRendezvousCluster rendezvousCluster,
                              ILogger logger)
        {
            this.logger = logger;
            this.membershipConfiguration = membershipConfiguration;
            pingReceived = new ManualResetEventSlim(false);
            newRendezvousConfiguration = new ManualResetEventSlim(false);
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
            this.clusterMembership = clusterMembership;
            this.rendezvousCluster = rendezvousCluster;
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
            newRendezvousConfiguration.Dispose();
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

                        var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
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
            const int rendezvousConfigurationChanged = 1;
            var result = WaitHandle.WaitAny(new[]
                                            {
                                                pingReceived.WaitHandle,
                                                newRendezvousConfiguration.WaitHandle
                                            },
                                            membershipConfiguration.PingSilenceBeforeRendezvousFailover);
            if (result == WaitHandle.WaitTimeout)
            {
                rendezvousCluster.RotateRendezvousServers();
                return true;
            }
            if (result == rendezvousConfigurationChanged)
            {
                newRendezvousConfiguration.Reset();
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
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateSubscriberSocket();
            socket.Connect(rendezvousServer.MulticastUri);
            socket.Subscribe();

            logger.Info($"Connected to Rendezvous {rendezvousServer.MulticastUri.AbsoluteUri}");

            return socket;
        }

        private ISocket CreateClusterMonitorSendingSocket()
        {
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
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
            => Ping(message, routerNotificationSocket)
               || Pong(message)
               || RendezvousReconfiguration(message)
               || MessageRoutingControlMessage(message, routerNotificationSocket)
               || RendezvousNotLeader(message);

        private bool RendezvousReconfiguration(IMessage message)
        {
            var shouldHandle = IsRendezvousReconfiguration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RendezvousConfigurationChangedMessage>();
                rendezvousCluster.Reconfigure(payload
                                                  .RendezvousNodes
                                                  .Select(rn => new RendezvousEndpoint
                                                                {
                                                                    UnicastUri = new Uri(rn.UnicastUri),
                                                                    MulticastUri = new Uri(rn.MulticastUri)
                                                                }));
                newRendezvousConfiguration.Set();
            }

            return shouldHandle;
        }

        private bool RendezvousNotLeader(IMessage message)
        {
            var shouldHandle = IsRendezvousNotLeader(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RendezvousNotLeaderMessage>();
                var newLeader = new RendezvousEndpoint
                                {
                                    MulticastUri = new Uri(payload.NewLeader.MulticastUri),
                                    UnicastUri = new Uri(payload.NewLeader.UnicastUri)
                                };
                if (!rendezvousCluster.GetCurrentRendezvousServer().Equals(newLeader))
                {
                    rendezvousCluster.SetCurrentRendezvousServer(newLeader);
                    newRendezvousConfiguration.Set();
                }
            }

            return shouldHandle;
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

        private bool MessageRoutingControlMessage(IMessage message, ISocket routerNotificationSocket)
        {
            var shouldHandle = IsRequestAllMessageRoutingMessage(message)
                               || IsRequestNodeMessageRoutingMessage(message)
                               || IsUnregisterMessageRoutingMessage(message)
                               || IsRegisterExternalRoute(message)
                               || IsUnregisterRoutingMessage(message)
                               || IsDiscoverMessageRouteMessage(message);

            if (shouldHandle)
            {
                if (IsRegisterExternalRoute(message))
                {
                    AddClusterMember(message);
                }
                if (IsUnregisterRoutingMessage(message))
                {
                    var payload = message.GetPayload<UnregisterNodeMessageRouteMessage>();
                    clusterMembership.DeleteClusterMember(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
                }

                routerNotificationSocket.SendMessage(message);
            }

            return shouldHandle;
        }

        public void RegisterSelf(IEnumerable<IMessageIdentifier> messageHandlers)
        {
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             MessageContracts = messageHandlers.Select(mi => new MessageContract
                                                                                             {
                                                                                                 Version = mi.Version,
                                                                                                 Identity = mi.Identity
                                                                                             }).ToArray()
                                         });
            outgoingMessages.Add(message);
        }

        public void UnregisterSelf(IEnumerable<IMessageIdentifier> messageIdentifiers)
        {
            var message = Message.Create(new UnregisterMessageRouteMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             MessageContracts = messageIdentifiers
                                                 .Select(mi => new MessageContract
                                                               {
                                                                   Identity = mi.Identity,
                                                                   Version = mi.Version
                                                               }
                                                 )
                                                 .ToArray()
                                         });
            outgoingMessages.Add(message);
        }

        public void RequestClusterRoutes()
        {
            var message = Message.Create(new RequestClusterMessageRoutesMessage
                                         {
                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress()
                                         });
            outgoingMessages.Add(message);
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembership.GetClusterMembers();

        public void DiscoverMessageRoute(IMessageIdentifier messageIdentifier)
        {
            var message = Message.Create(new DiscoverMessageRouteMessage
                                         {
                                             RequestorSocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             RequestorUri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             MessageContract = new MessageContract
                                                               {
                                                                   Version = messageIdentifier.Version,
                                                                   Identity = messageIdentifier.Identity
                                                               }
                                         });
            outgoingMessages.Add(message);
        }

        private IMessage CreateUnregisterRoutingMessage()
            => Message.Create(new UnregisterNodeMessageRouteMessage
                              {
                                  Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                  SocketIdentity = routerConfiguration.ScaleOutAddress.Identity
                              });

        private bool IsRegisterExternalRoute(IMessage message)
        {
            if (Unsafe.Equals(RegisterExternalMessageRouteMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsDiscoverMessageRouteMessage(IMessage message)
        {
            if (Unsafe.Equals(DiscoverMessageRouteMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<DiscoverMessageRouteMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
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

        private bool IsUnregisterRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(UnregisterNodeMessageRouteMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<UnregisterNodeMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsUnregisterMessageRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(UnregisterMessageRouteMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsRendezvousNotLeader(IMessage message)
            => Unsafe.Equals(RendezvousNotLeaderMessage.MessageIdentity, message.Identity);

        private bool IsRendezvousReconfiguration(IMessage message)
            => Unsafe.Equals(RendezvousConfigurationChangedMessage.MessageIdentity, message.Identity);

        private bool IsRequestAllMessageRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(RequestClusterMessageRoutesMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<RequestClusterMessageRoutesMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
            }

            return false;
        }

        private bool IsRequestNodeMessageRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(RequestNodeMessageRoutesMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();

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
                                         });
            outgoingMessages.Add(message);
        }

        private void AddClusterMember(IMessage message)
        {
            var registration = message.GetPayload<RegisterExternalMessageRouteMessage>();
            var clusterMember = new SocketEndpoint(new Uri(registration.Uri), registration.SocketIdentity);
            clusterMembership.AddClusterMember(clusterMember);
        }

        private void ProcessPongMessage(IMessage message)
        {
            var payload = message.GetPayload<PongMessage>();

            var nodeNotFound = clusterMembership.KeepAlive(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
            if (!nodeNotFound)
            {
                RequestNodeMessageHandlersRouting(payload);
            }
        }

        private void RequestNodeMessageHandlersRouting(PongMessage payload)
        {
            var request = Message.Create(new RequestNodeMessageRoutesMessage
                                         {
                                             TargetNodeIdentity = payload.SocketIdentity,
                                             TargetNodeUri = payload.Uri
                                         });
            outgoingMessages.Add(request);

            logger.Debug("Route not found. Requesting registrations for " +
                         $"Uri:{payload.Uri} " +
                         $"Socket:{payload.SocketIdentity.GetString()}");
        }

        private void UnregisterDeadNodes(ISocket routerNotificationSocket, DateTime pingTime, TimeSpan pingInterval)
        {
            foreach (var deadNode in clusterMembership.GetDeadMembers(pingTime, pingInterval))
            {
                var message = Message.Create(new UnregisterNodeMessageRouteMessage
                                             {
                                                 Uri = deadNode.Uri.ToSocketAddress(),
                                                 SocketIdentity = deadNode.Identity
                                             });
                clusterMembership.DeleteClusterMember(deadNode);
                routerNotificationSocket.SendMessage(message);
            }
        }
    }
}