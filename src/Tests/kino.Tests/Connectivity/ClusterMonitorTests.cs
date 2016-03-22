using System;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMonitorTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private ClusterMonitorSocketFactory clusterMonitorSocketFactory;
        private Mock<ILogger> logger;
        private Mock<ISocketFactory> socketFactory;
        private RouterConfiguration routerConfiguration;
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private ClusterMembershipConfiguration clusterMembershipConfiguration;
        private IClusterMessageSender clusterMessageSender;
        private IClusterMessageListener clusterMessageListener;
        private Mock<IRouteDiscovery> routeDiscovery;

        [SetUp]
        public void Setup()
        {
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            logger = new Mock<ILogger>();
            routeDiscovery = new Mock<IRouteDiscovery>();
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
                                                 PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(2),
                                                 PongSilenceBeforeRouteDeletion = TimeSpan.FromMilliseconds(4),
                                                 RouteDiscovery = new RouteDiscoveryConfiguration
                                                                  {
                                                                      MaxRequestsQueueLength = 100,
                                                                      RequestsPerSend = 10,
                                                                      SendingPeriod = TimeSpan.FromSeconds(1)
                                                                  }
                                             };
            clusterMessageSender = new ClusterMessageSender(rendezvousCluster.Object,
                                                            routerConfiguration,
                                                            socketFactory.Object,
                                                            logger.Object);
            clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                socketFactory.Object,
                                                                routerConfiguration,
                                                                clusterMessageSender,
                                                                clusterMembership.Object,
                                                                clusterMembershipConfiguration,
                                                                logger.Object);
        }

        [Test]
        public void RegisterSelf_SendRegistrationMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMessageSender,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object);
            try
            {
                clusterMonitor.Start();

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                clusterMonitor.RegisterSelf(new[] {messageIdentifier});

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(message.Equals(KinoMessages.RegisterExternalMessageRoute));
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, routerConfiguration.ScaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(payload.MessageContracts.Any(mc => Unsafe.Equals(mc.Identity, messageIdentifier.Identity)
                                                                 && Unsafe.Equals(mc.Version, messageIdentifier.Version)));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void UnregisterSelf_SendUnregisterMessageRouteMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMessageSender,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object);
            try
            {
                clusterMonitor.Start();

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion);
                clusterMonitor.UnregisterSelf(new[] {messageIdentifier});

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(message.Equals(KinoMessages.UnregisterMessageRoute));
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, routerConfiguration.ScaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(payload.MessageContracts.Any(mc => Unsafe.Equals(mc.Identity, messageIdentifier.Identity)
                                                                 && Unsafe.Equals(mc.Version, messageIdentifier.Version)));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void RequestClusterRoutes_SendRequestClusterMessageRoutesMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMessageSender,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object);
            try
            {
                clusterMonitor.Start();

                clusterMonitor.RequestClusterRoutes();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(message.Equals(KinoMessages.RequestClusterMessageRoutes));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void DiscoverMessageRoute_SendDiscoverMessageRouteMessageToRendezvous()
        {
            clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(10);
            clusterMembershipConfiguration.PongSilenceBeforeRouteDeletion = TimeSpan.FromSeconds(10);
            clusterMembershipConfiguration.RouteDiscovery.SendingPeriod = TimeSpan.FromMilliseconds(10);

            var clusterMonitor = new ClusterMonitor(routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMessageSender,
                                                    clusterMessageListener,
                                                    new RouteDiscovery(clusterMessageSender,
                                                                       routerConfiguration,
                                                                       clusterMembershipConfiguration,
                                                                       logger.Object));
            try
            {
                clusterMonitor.Start();

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, Guid.NewGuid().ToByteArray());
                clusterMonitor.DiscoverMessageRoute(messageIdentifier);

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(TimeSpan.FromSeconds(2));

                Assert.IsNotNull(message);
                Assert.IsTrue(message.Equals(KinoMessages.DiscoverMessageRoute));
                var payload = message.GetPayload<DiscoverMessageRouteMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.RequestorSocketIdentity, routerConfiguration.ScaleOutAddress.Identity));
                Assert.AreEqual(payload.RequestorUri, routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(Unsafe.Equals(payload.MessageContract.Identity, messageIdentifier.Identity));
                Assert.IsTrue(Unsafe.Equals(payload.MessageContract.Version, messageIdentifier.Version));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }
    }
}