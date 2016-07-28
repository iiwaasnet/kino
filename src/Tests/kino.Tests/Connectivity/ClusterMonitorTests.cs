﻿using System;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
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
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);
        private ClusterMonitorSocketFactory clusterMonitorSocketFactory;
        private Mock<ILogger> logger;
        private Mock<ISocketFactory> socketFactory;
        private RouterConfiguration routerConfiguration;
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private ClusterMembershipConfiguration clusterMembershipConfiguration;
        private IClusterMessageSender clusterMessageSender;
        private IClusterMessageListener clusterMessageListener;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private Mock<IRouteDiscovery> routeDiscovery;
        private Mock<ISecurityProvider> securityProvider;
        private string securityDomain;

        [SetUp]
        public void Setup()
        {
            securityProvider = new Mock<ISecurityProvider>();
            securityDomain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetAllowedSecurityDomains()).Returns(new[] {securityDomain});
            securityProvider.Setup(m => m.GetSecurityDomain(It.IsAny<byte[]>())).Returns(securityDomain);
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            logger = new Mock<ILogger>();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
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
                                                            performanceCounterManager.Object,
                                                            securityProvider.Object,
                                                            logger.Object);
            clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                socketFactory.Object,
                                                                routerConfiguration,
                                                                clusterMessageSender,
                                                                clusterMembership.Object,
                                                                clusterMembershipConfiguration,
                                                                performanceCounterManager.Object,
                                                                securityProvider.Object,
                                                                logger.Object);
        }

        [Test]
        public void RegisterSelf_SendRegistrationMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMessageSender,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object,
                                                    securityProvider.Object);
            try
            {
                clusterMonitor.Start(StartTimeout);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                var messageContract = Guid.NewGuid().ToString();
                clusterMonitor.RegisterSelf(new[] {messageIdentifier}, messageContract);

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(message.Equals(KinoMessages.RegisterExternalMessageRoute));
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, routerConfiguration.ScaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(payload.MessageContracts.Any(mc => Unsafe.Equals(mc.Identity, messageIdentifier.Identity)
                                                                 && Unsafe.Equals(mc.Version, messageIdentifier.Version)
                                                                 && Unsafe.Equals(mc.Partition, messageIdentifier.Partition)));
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
                                                    routeDiscovery.Object,
                                                    securityProvider.Object);
            try
            {
                clusterMonitor.Start(StartTimeout);

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
                                                                 && Unsafe.Equals(mc.Version, messageIdentifier.Version)
                                                                 && Unsafe.Equals(mc.Partition, messageIdentifier.Partition)));
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
                                                    routeDiscovery.Object,
                                                    securityProvider.Object);
            try
            {
                clusterMonitor.Start(StartTimeout);

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
                                                                       securityProvider.Object,
                                                                       logger.Object),
                                                    securityProvider.Object);
            try
            {
                clusterMonitor.Start(StartTimeout);

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, Guid.NewGuid().ToByteArray(), IdentityExtensions.Empty);
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
                Assert.IsTrue(Unsafe.Equals(payload.MessageContract.Partition, messageIdentifier.Partition));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }
    }
}