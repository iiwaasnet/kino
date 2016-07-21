using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity
{
    public class ClusterMessageListener : IClusterMessageListener
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
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISecurityProvider securityProvider;

        public ClusterMessageListener(IRendezvousCluster rendezvousCluster,
                                      ISocketFactory socketFactory,
                                      RouterConfiguration routerConfiguration,
                                      IClusterMessageSender clusterMessageSender,
                                      IClusterMembership clusterMembership,
                                      ClusterMembershipConfiguration membershipConfiguration,
                                      IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                      ISecurityProvider securityProvider,
                                      ILogger logger)
        {
            this.logger = logger;
            this.membershipConfiguration = membershipConfiguration;
            this.performanceCounterManager = performanceCounterManager;
            this.securityProvider = securityProvider;
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
                var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
                logger.Info($"Ping timeout Rendezvous {rendezvousServer.BroadcastUri.AbsoluteUri}");

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
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.ClusterListenerInternalSocketSendRate);
            SocketHelper.SafeConnect(() => socket.Connect(routerConfiguration.RouterAddress.Uri));

            return socket;
        }

        private ISocket CreateClusterMonitorSubscriptionSocket()
        {
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.ClusterListenerSocketReceiveRate);
            socket.Connect(rendezvousServer.BroadcastUri);
            socket.Subscribe();

            logger.Info($"Connected to Rendezvous {rendezvousServer.BroadcastUri.AbsoluteUri}");

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
                var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
                logger.Info("New Rendezvous cluster configuration. " +
                            $"Disconnecting {rendezvousServer.BroadcastUri.AbsoluteUri}");

                var payload = message.GetPayload<RendezvousConfigurationChangedMessage>();
                rendezvousCluster.Reconfigure(payload
                                                  .RendezvousNodes
                                                  .Select(rn => new RendezvousEndpoint(new Uri(rn.UnicastUri),
                                                                                       new Uri(rn.MulticastUri))));
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
                var newLeader = new RendezvousEndpoint(new Uri(payload.NewLeader.UnicastUri),
                                                       new Uri(payload.NewLeader.MulticastUri));
                var currentLeader = rendezvousCluster.GetCurrentRendezvousServer();
                if (!currentLeader.Equals(newLeader))
                {
                    logger.Info($"New Rendezvous leader: {newLeader.BroadcastUri.AbsoluteUri}. " +
                                $"Disconnecting {currentLeader.BroadcastUri.AbsoluteUri}");

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
                               || IsUnregisterNodeMessage(message)
                               || IsDiscoverMessageRouteMessage(message);

            if (shouldHandle)
            {
                routerNotificationSocket.SendMessage(message);
            }

            return shouldHandle;
        }

        private bool IsDiscoverMessageRouteMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.DiscoverMessageRoute))
            {
                var payload = message.GetPayload<DiscoverMessageRouteMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
            }

            return false;
        }

        private bool IsPing(IMessage message)
            => message.Equals(KinoMessages.Ping);

        private bool IsRendezvousNotLeader(IMessage message)
            => message.Equals(KinoMessages.RendezvousNotLeader);

        private bool IsRendezvousReconfiguration(IMessage message)
            => message.Equals(KinoMessages.RendezvousConfigurationChanged);

        private bool IsRequestAllMessageRoutingMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.RequestClusterMessageRoutes))
            {
                var payload = message.GetPayload<RequestClusterMessageRoutesMessage>();

                return !ThisNodeSocket(payload.RequestorSocketIdentity);
            }

            return false;
        }

        private bool IsPong(IMessage message)
        {
            if (message.Equals(KinoMessages.Pong))
            {
                var payload = message.GetPayload<PongMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsRequestNodeMessageRoutingMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.RequestNodeMessageRoutes))
            {
                var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();

                return ThisNodeSocket(payload.TargetNodeIdentity);
            }

            return false;
        }

        private bool IsUnregisterNodeMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.UnregisterNode))
            {
                var payload = message.GetPayload<UnregisterNodeMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return message.Equals(KinoMessages.UnregisterUnreachableNode);
        }

        private bool IsRegisterExternalRoute(IMessage message)
        {
            if (message.Equals(KinoMessages.RegisterExternalMessageRoute))
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();

                return !ThisNodeSocket(payload.SocketIdentity);
            }

            return false;
        }

        private bool IsUnregisterMessageRoutingMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.UnregisterMessageRoute))
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
            foreach (var securityDomain in securityProvider.GetAllowedSecurityDomains())
            {
                var message = Message.Create(new PongMessage
                                             {
                                                 Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                                 SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                                                 PingId = pingId
                                             },
                                             securityDomain);
                message.As<Message>().SignMessage(securityProvider);

                clusterMessageSender.EnqueueMessage(message);
            }
        }

        private void UnregisterDeadNodes(ISocket routerNotificationSocket, DateTime pingTime, TimeSpan pingInterval)
        {
            foreach (var deadNode in clusterMembership.GetDeadMembers(pingTime, pingInterval))
            {
                var message = Message.Create(new UnregisterUnreachableNodeMessage
                                             {
                                                 Uri = deadNode.Uri.ToSocketAddress(),
                                                 SocketIdentity = deadNode.Identity
                                             });
                routerNotificationSocket.SendMessage(message);
            }
        }

        private void ProcessPongMessage(IMessage message)
        {
            if (securityProvider.SecurityDomainIsAllowed(message.SecurityDomain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                var payload = message.GetPayload<PongMessage>();
                var nodeFound = clusterMembership.KeepAlive(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
                if (!nodeFound)
                {
                    RequestNodeMessageHandlersRouting(payload);
                }
            }
        }

        private void RequestNodeMessageHandlersRouting(PongMessage payload)
        {
            foreach (var securityDomain in securityProvider.GetAllowedSecurityDomains())
            {
                var request = Message.Create(new RequestNodeMessageRoutesMessage
                                             {
                                                 TargetNodeIdentity = payload.SocketIdentity,
                                                 TargetNodeUri = payload.Uri
                                             },
                                             securityDomain);
                request.As<Message>().SignMessage(securityProvider);
                clusterMessageSender.EnqueueMessage(request);
            }

            logger.Debug("Route not found. Requesting registrations for " +
                         $"Uri:{payload.Uri} " +
                         $"Socket:{payload.SocketIdentity.GetAnyString()}");
        }
    }
}