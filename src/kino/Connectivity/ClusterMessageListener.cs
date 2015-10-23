using System;
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
    internal class ClusterMessageListener : IClusterMessageListener
    {
        private readonly ILogger logger;
        private readonly IRendezvousCluster rendezvousCluster;
        private readonly ISocketFactory socketFactory;
        private readonly RouterConfiguration routerConfiguration;
        private readonly IClusterMessageSender clusterMessageSender;
        private readonly ManualResetEventSlim pingReceived;
        private readonly ManualResetEventSlim newRendezvousConfiguration;
        private readonly IClusterMembership clusterMembership;
        private readonly ClusterMembershipConfiguration membershipConfiguration;        

        public ClusterMessageListener(IRendezvousCluster rendezvousCluster,
                                      ISocketFactory socketFactory,
                                      RouterConfiguration routerConfiguration,
                                      IClusterMessageSender clusterMessageSender,
                                      IClusterMembership clusterMembership,
                                      ClusterMembershipConfiguration membershipConfiguration,
                                      ILogger logger)
        {
            this.logger = logger;
            this.membershipConfiguration = membershipConfiguration;
            this.rendezvousCluster = rendezvousCluster;
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
            this.clusterMessageSender = clusterMessageSender;
            this.clusterMembership = clusterMembership;
            pingReceived = new ManualResetEventSlim(false);
            newRendezvousConfiguration = new ManualResetEventSlim(false);
        }

        public void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway)
        {
            try
            {
                StartRendezvousMonitoring(restartRequestHandler, token);

                using (var clusterMonitorSubscriptionSocket = CreateClusterMonitorSubscriptionSocket())
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

        private void StartRendezvousMonitoring(Action restartRequestHandler, CancellationToken token)
        {
            pingReceived.Reset();
            newRendezvousConfiguration.Reset();

            Task.Factory.StartNew(_ => RendezvousConnectionMonitor(restartRequestHandler, token),
                                  TaskCreationOptions.LongRunning,
                                  token);
        }

        private void RendezvousConnectionMonitor(Action restartRequestHandler, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (PingSilence())
                    {
                        var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
                        logger.Info($"Disconnecting from Rendezvous {rendezvousServer.MulticastUri.AbsoluteUri}");

                        restartRequestHandler();
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

        private ISocket CreateRouterCommunicationSocket()
        {
            var socket = socketFactory.CreateDealerSocket();
            socket.Connect(routerConfiguration.RouterAddress.Uri);

            return socket;
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

        private bool IsDiscoverMessageRouteMessage(IMessage message)
        {
            if (Unsafe.Equals(MessageIdentifiers.DiscoverMessageRoute.Identity, message.Identity))
            {
                var payload = message.GetPayload<DiscoverMessageRouteMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
            }

            return false;
        }

        private bool IsPing(IMessage message)
            => Unsafe.Equals(PingMessage.MessageIdentity, message.Identity);

        private bool IsRendezvousNotLeader(IMessage message)
            => Unsafe.Equals(RendezvousNotLeaderMessage.MessageIdentity, message.Identity);

        private bool IsRendezvousReconfiguration(IMessage message)
            => Unsafe.Equals(RendezvousConfigurationChangedMessage.MessageIdentity, message.Identity);

        private bool IsRequestAllMessageRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(MessageIdentifiers.RequestClusterMessageRoutes.Identity, message.Identity))
            {
                var payload = message.GetPayload<RequestClusterMessageRoutesMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
            }

            return false;
        }

        private bool IsPong(IMessage message)
        {
            if (Unsafe.Equals(PongMessage.MessageIdentity, message.Identity))
            {
                var payload = message.GetPayload<PongMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsRequestNodeMessageRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(MessageIdentifiers.RequestNodeMessageRoutes.Identity, message.Identity))
            {
                var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();

                return ThisNodeSocket(payload.TargetNodeIdentity);
            }

            return false;
        }

        private bool IsUnregisterRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(MessageIdentifiers.UnregisterNodeMessageRoute.Identity, message.Identity))
            {
                var payload = message.GetPayload<UnregisterNodeMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsRegisterExternalRoute(IMessage message)
        {
            if (Unsafe.Equals(MessageIdentifiers.RegisterExternalMessageRoute.Identity, message.Identity))
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsUnregisterMessageRoutingMessage(IMessage message)
        {
            if (Unsafe.Equals(MessageIdentifiers.UnregisterMessageRoute.Identity, message.Identity))
            {
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool ThisNodeSocket(byte[] socketIdentity)
            => Unsafe.Equals(routerConfiguration.ScaleOutAddress.Identity, socketIdentity);

        private void SendPong(ulong pingId)
        {
            var message = Message.Create(new PongMessage
                                         {
                                             Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                             SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                             PingId = pingId
                                         });
            clusterMessageSender.EnqueueMessage(message);
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
            clusterMessageSender.EnqueueMessage(request);

            logger.Debug("Route not found. Requesting registrations for " +
                         $"Uri:{payload.Uri} " +
                         $"Socket:{payload.SocketIdentity.GetString()}");
        }
    }
}