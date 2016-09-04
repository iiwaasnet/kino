using System;
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
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private ClusterMembershipConfiguration clusterMembershipConfiguration;
        private IClusterMessageSender clusterMessageSender;
        private IClusterMessageListener clusterMessageListener;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private Mock<IRouteDiscovery> routeDiscovery;
        private Mock<ISecurityProvider> securityProvider;
        private string domain;
        private ClusterMonitor clusterMonitor;
        private Mock<IRouterConfigurationProvider> routerConfigurationProvider;
        private SocketEndpoint scaleOutAddress;

        [SetUp]
        public void Setup()
        {
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            logger = new Mock<ILogger>();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            routeDiscovery = new Mock<IRouteDiscovery>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            rendezvousCluster = new Mock<IRendezvousCluster>();
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
            var routerConfiguration = new RouterConfiguration
                                      {
                                          RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateIdentity())
                                      };
            scaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), SocketIdentifier.CreateIdentity());
            routerConfigurationProvider = new Mock<IRouterConfigurationProvider>();
            routerConfigurationProvider.Setup(m => m.GetRouterConfiguration()).Returns(routerConfiguration);
            routerConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            clusterMessageSender = new ClusterMessageSender(rendezvousCluster.Object,
                                                            routerConfigurationProvider.Object,
                                                            socketFactory.Object,
                                                            performanceCounterManager.Object,
                                                            securityProvider.Object,
                                                            logger.Object);
            clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                socketFactory.Object,
                                                                routerConfigurationProvider.Object,
                                                                clusterMessageSender,
                                                                clusterMembership.Object,
                                                                clusterMembershipConfiguration,
                                                                performanceCounterManager.Object,
                                                                securityProvider.Object,
                                                                logger.Object);
            clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
                                                clusterMembership.Object,
                                                clusterMessageSender,
                                                clusterMessageListener,
                                                routeDiscovery.Object,
                                                securityProvider.Object);
        }

        [Test]
        public void RegisterSelf_SendRegistrationMessageToRendezvous()
        {
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
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, scaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
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
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, scaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
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

            var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
                                                    clusterMembership.Object,
                                                    clusterMessageSender,
                                                    clusterMessageListener,
                                                    new RouteDiscovery(clusterMessageSender,
                                                                       routerConfigurationProvider.Object,
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
                Assert.IsTrue(Unsafe.Equals(payload.RequestorSocketIdentity, scaleOutAddress.Identity));
                Assert.AreEqual(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(Unsafe.Equals(payload.MessageContract.Identity, messageIdentifier.Identity));
                Assert.IsTrue(Unsafe.Equals(payload.MessageContract.Version, messageIdentifier.Version));
                Assert.IsTrue(Unsafe.Equals(payload.MessageContract.Partition, messageIdentifier.Partition));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void UnregisterSelf_SendsUnregisterMessagesForAllIdentitiesGroupedByDomain()
        {
            var messageHubs = new[]
                              {
                                  new MessageIdentifier(Guid.NewGuid().ToByteArray()),
                                  new MessageIdentifier(Guid.NewGuid().ToByteArray()),
                                  new MessageIdentifier(Guid.NewGuid().ToByteArray())
                              };
            var messageHandlers = new[]
                                  {
                                      MessageIdentifier.Create<SimpleMessage>(),
                                      MessageIdentifier.Create<AsyncMessage>()
                                  };
            var allowedDomains = new[]
                                 {
                                     Guid.NewGuid().ToString(),
                                     Guid.NewGuid().ToString(),
                                     Guid.NewGuid().ToString(),
                                     Guid.NewGuid().ToString()
                                 };
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            foreach (var domain in allowedDomains)
            {
                securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            }
            securityProvider.Setup(m => m.GetDomain(messageHandlers.First().Identity)).Returns(allowedDomains.First());
            securityProvider.Setup(m => m.GetDomain(messageHandlers.Second().Identity)).Returns(allowedDomains.Second());
            var clusterMessageSender = new Mock<IClusterMessageSender>();
            var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
                                                    clusterMembership.Object,
                                                    clusterMessageSender.Object,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object,
                                                    securityProvider.Object);
            //
            clusterMonitor.UnregisterSelf(messageHandlers.Concat(messageHubs));
            //
            clusterMessageSender.Verify(m => m.EnqueueMessage(It.IsAny<IMessage>()), Times.Exactly(allowedDomains.Count()));
            foreach (var domain in allowedDomains)
            {
                clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => msg.Domain == domain)));
            }
        }

        [Test]
        public void UnregisterSelf_SendsUnregistrationMessageForEachMessageHubAndDomain()
        {
            var messageHubs = new[]
                              {
                                  new MessageIdentifier(Guid.NewGuid().ToByteArray()),
                                  new MessageIdentifier(Guid.NewGuid().ToByteArray()),
                                  new MessageIdentifier(Guid.NewGuid().ToByteArray())
                              };
            var messageHandlers = new[]
                                  {
                                      MessageIdentifier.Create<SimpleMessage>(),
                                      MessageIdentifier.Create<AsyncMessage>()
                                  };
            var allowedDomains = new[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            foreach (var domain in allowedDomains)
            {
                securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            }
            securityProvider.Setup(m => m.GetDomain(messageHandlers.First().Identity)).Returns(allowedDomains.First());
            securityProvider.Setup(m => m.GetDomain(messageHandlers.Second().Identity)).Returns(allowedDomains.Second());
            var clusterMessageSender = new Mock<IClusterMessageSender>();
            var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
                                                    clusterMembership.Object,
                                                    clusterMessageSender.Object,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object,
                                                    securityProvider.Object);
            //
            clusterMonitor.UnregisterSelf(messageHandlers.Concat(messageHubs));
            //
            foreach (var domain in allowedDomains)
            {
                foreach (var messageHub in messageHubs)
                {
                    clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => AreEqual(msg, domain, messageHub))));
                }
            }

            clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => AreEqual(msg, allowedDomains.First(), messageHandlers.First()))));
            clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => AreEqual(msg, allowedDomains.Second(), messageHandlers.Second()))));
        }

        private static bool AreEqual(IMessage msg, string domain, MessageIdentifier messageHub)
        {
            return msg.GetPayload<UnregisterMessageRouteMessage>()
                      .MessageContracts
                      .Any(mc => Unsafe.Equals(mc.Identity, messageHub.Identity))
                   && msg.Domain == domain;
        }

        [Test]
        public void RequestClusterMessageRoutesMessage_IsSentOncePerEachAllowedDomain()
        {
            var allowedDomains = new[] {domain, Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            var clusterMessageSender = new Mock<IClusterMessageSender>();
            var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
                                                    clusterMembership.Object,
                                                    clusterMessageSender.Object,
                                                    clusterMessageListener,
                                                    routeDiscovery.Object,
                                                    securityProvider.Object);
            //
            clusterMonitor.RequestClusterRoutes();
            //
            clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => allowedDomains.Contains(msg.Domain))), Times.Exactly(allowedDomains.Length));
        }
    }
}