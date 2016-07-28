using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMessageListenetTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private ClusterMonitorSocketFactory clusterMonitorSocketFactory;
        private Mock<ILogger> logger;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ISocketFactory> socketFactory;
        private RouterConfiguration routerConfiguration;
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private Mock<IClusterMessageSender> clusterMessageSender;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private ClusterMembershipConfiguration clusterMembershipConfiguration;
        private string securityDomain;

        [SetUp]
        public void Setup()
        {
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            clusterMessageSender = new Mock<IClusterMessageSender>();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            logger = new Mock<ILogger>();

            securityDomain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.SecurityDomainIsAllowed(It.IsAny<string>())).Returns(true);
            securityProvider.Setup(m => m.GetAllowedSecurityDomains()).Returns(new[] {securityDomain});

            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            rendezvousCluster = new Mock<IRendezvousCluster>();
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), SocketIdentifier.CreateIdentity()),
                                      RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateIdentity())
                                  };
            var rendezvousEndpoint = new RendezvousEndpoint(new Uri("tcp://127.0.0.1:5000"),
                                                            new Uri("tcp://127.0.0.1:5000"));
            rendezvousCluster.Setup(m => m.GetCurrentRendezvousServer()).Returns(rendezvousEndpoint);
            clusterMembership = new Mock<IClusterMembership>();
            clusterMembershipConfiguration = new ClusterMembershipConfiguration
                                             {
                                                 RunAsStandalone = false,
                                                 PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(4),
                                                 PongSilenceBeforeRouteDeletion = TimeSpan.FromMilliseconds(8)
                                             };
        }

        [Test]
        public void IfPingIsNotCommingInTime_SwitchToNextRendezvousServer()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            Thread.Sleep(WaitLongerThanPingSilenceFailover());
            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.GetCurrentRendezvousServer(), Times.AtLeastOnce);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.AtLeastOnce);
        }

        [Test]
        public void IfPingComesInTime_SwitchToNextRendezvousServerNeverHappens()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var ping = new PingMessage
                       {
                           PingId = 1L,
                           PingInterval = TimeSpan.FromSeconds(2)
                       };

            Thread.Sleep(WaitLessThanPingSilenceFailover());
            socket.DeliverMessage(Message.Create(ping));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.SetCurrentRendezvousServer(It.IsAny<RendezvousEndpoint>()), Times.Never);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.Never);
            rendezvousCluster.Verify(m => m.GetCurrentRendezvousServer(), Times.Once());
        }

        [Test]
        public void IfNonLeaderMessageArrives_NewLeaderIsSelectedFromReceivedMessage()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var notLeaderMessage = new RendezvousNotLeaderMessage
                                   {
                                       NewLeader = new RendezvousNode
                                                   {
                                                       MulticastUri = "tpc://127.0.0.2:6000",
                                                       UnicastUri = "tpc://127.0.0.2:6000"
                                                   }
                                   };
            socket.DeliverMessage(Message.Create(notLeaderMessage));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.SetCurrentRendezvousServer(It.Is<RendezvousEndpoint>(e => SameServer(e, notLeaderMessage))),
                                     Times.Once());
            rendezvousCluster.Verify(m => m.GetCurrentRendezvousServer(), Times.AtLeastOnce);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.Never);
        }

        [Test]
        public void PongMessage_RenewesRegistrationOfSourceNode()
        {
            var sourceNode = new SocketEndpoint(new Uri("tpc://127.0.0.3:7000"), SocketIdentifier.CreateIdentity());

            clusterMembership.Setup(m => m.KeepAlive(sourceNode)).Returns(true);

            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var pong = new PongMessage
                       {
                           PingId = 1L,
                           SocketIdentity = sourceNode.Identity,
                           Uri = sourceNode.Uri.ToSocketAddress()
                       };
            socket.DeliverMessage(Message.Create(pong));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            clusterMembership.Verify(m => m.KeepAlive(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == sourceNode.Uri.ToSocketAddress()
                                                                                 && Unsafe.Equals(e.Identity, sourceNode.Identity))),
                                     Times.Once());
        }

        [Test]
        public void IfPongMessageComesFromUnknownNodeInSupportedDomain_RequestNodeMessageRoutesMessageSent()
        {
            SendPongMessageFromFromUnknownNode(true, Times.Once);
        }

        [Test]
        public void IfPongMessageComesFromUnknownNodeInNonSupportedDomain_NoRequestNodeMessageRoutesMessageSent()
        {
            SendPongMessageFromFromUnknownNode(false, Times.Never);
        }

        private void SendPongMessageFromFromUnknownNode(bool domainIsSupported, Func<Times> times)
        {
            securityProvider.Setup(m => m.SecurityDomainIsAllowed(It.IsAny<string>())).Returns(domainIsSupported);
            var sourceNode = new SocketEndpoint(new Uri("tpc://127.0.0.3:7000"), SocketIdentifier.CreateIdentity());

            clusterMembership.Setup(m => m.KeepAlive(sourceNode)).Returns(false);

            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var pong = new PongMessage
                       {
                           PingId = 1L,
                           SocketIdentity = sourceNode.Identity,
                           Uri = sourceNode.Uri.ToSocketAddress()
                       };
            var routesRequestMessage = new RequestNodeMessageRoutesMessage
                                       {
                                           TargetNodeIdentity = pong.SocketIdentity,
                                           TargetNodeUri = pong.Uri
                                       };
            socket.DeliverMessage(Message.Create(pong));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            clusterMembership.Verify(m => m.KeepAlive(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == sourceNode.Uri.ToSocketAddress()
                                                                                 && Unsafe.Equals(e.Identity, sourceNode.Identity))),
                                     times);
            clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => RoutesRequestMessage(msg, routesRequestMessage))), times);
        }

        [Test]
        public void IfRendezvousReconfigurationMessageArrives_RendezvousClusterIsChanged()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var newRendezouvEndpoint = new RendezvousEndpoint(new Uri("tcp://192.0.0.1:8000"),
                                                              new Uri("tcp://192.0.0.1:8001"));
            var message = Message.Create(new RendezvousConfigurationChangedMessage
                                         {
                                             RendezvousNodes = new[]
                                                               {
                                                                   new RendezvousNode
                                                                   {
                                                                       UnicastUri = newRendezouvEndpoint.UnicastUri.AbsoluteUri,
                                                                       MulticastUri = newRendezouvEndpoint.BroadcastUri.AbsoluteUri
                                                                   }
                                                               }
                                         });

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            socket.DeliverMessage(message);
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.Reconfigure(It.Is<IEnumerable<RendezvousEndpoint>>(ep => ep.Contains(newRendezouvEndpoint))),
                                     Times.Once);
        }

        [Test]
        public void RequestClusterMessageRoutesMessage_IsForwardedToMessageRouter()
        {
            var payload = new RequestClusterMessageRoutesMessage();
            MessageIsForwardedToMessageRouter(payload, KinoMessages.RequestClusterMessageRoutes);
        }

        [Test]
        public void UnregisterMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new UnregisterMessageRouteMessage
                          {
                              Uri = "tcp://127.1.1.1:5000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            MessageIsForwardedToMessageRouter(payload, KinoMessages.UnregisterMessageRoute);
        }

        [Test]
        public void UnregisterNodeMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new UnregisterNodeMessage
                          {
                              Uri = "tcp://127.0.0.3:6000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            MessageIsForwardedToMessageRouter(payload, KinoMessages.UnregisterNode);
        }

        [Test]
        public void DiscoverMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new DiscoverMessageRouteMessage
                          {
                              RequestorUri = "tcp://127.0.0.3:6000",
                              RequestorSocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            MessageIsForwardedToMessageRouter(payload, KinoMessages.DiscoverMessageRoute);
        }

        [Test]
        public void RegisterExternalMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new RegisterExternalMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.3:6000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            MessageIsForwardedToMessageRouter(payload, KinoMessages.RegisterExternalMessageRoute);
        }

        private void MessageIsForwardedToMessageRouter<TPayload>(TPayload payload, MessageIdentifier messageIdentity)
            where TPayload : IPayload
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    performanceCounterManager.Object,
                                                                    securityProvider.Object,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            socket.DeliverMessage(Message.Create(payload));

            var messageRouterMessage = clusterMonitorSocketFactory
                .GetRouterCommunicationSocket()
                .GetSentMessages()
                .BlockingLast(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            Assert.IsNotNull(messageRouterMessage);
            Assert.IsTrue(messageRouterMessage.Equals(messageIdentity));
        }

        private bool RoutesRequestMessage(IMessage message, RequestNodeMessageRoutesMessage routesRequestMessage)
        {
            var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();
            return Unsafe.Equals(payload.Identity, routesRequestMessage.Identity)
                   && payload.TargetNodeUri == routesRequestMessage.TargetNodeUri;
        }

        private TimeSpan WaitLessThanPingSilenceFailover()
            => TimeSpan.FromMilliseconds(clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover.TotalMilliseconds * 0.5);

        private TimeSpan WaitLongerThanPingSilenceFailover()
            =>
                TimeSpan.FromMilliseconds(clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover.TotalMilliseconds * 1.5);

        private static bool SameServer(RendezvousEndpoint e, RendezvousNotLeaderMessage notLeaderMessage)
            => e.BroadcastUri.ToSocketAddress() == notLeaderMessage.NewLeader.MulticastUri
               && e.UnicastUri.ToSocketAddress() == notLeaderMessage.NewLeader.UnicastUri;

        private Task StartListeningMessages(IClusterMessageListener clusterMessageListener, Action restartRequestAction, CancellationToken token)
            => Task.Factory.StartNew(() => clusterMessageListener.StartBlockingListenMessages(restartRequestAction, token, new Barrier(1)),
                                     TaskCreationOptions.LongRunning);

        private Task StartListeningMessages(IClusterMessageListener clusterMessageListener, CancellationToken token)
            => StartListeningMessages(clusterMessageListener, () => { }, token);
    }
}