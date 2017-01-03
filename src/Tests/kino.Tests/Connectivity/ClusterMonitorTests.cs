using System;
using System.Linq;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMonitorTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);
        private Mock<ILogger> logger;
        private Mock<IRouteDiscovery> routeDiscovery;
        private Mock<ISecurityProvider> securityProvider;
        private string domain;
        private ClusterMonitor clusterMonitor;
        private SocketEndpoint scaleOutAddress;
        private Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private Mock<IAutoDiscoverySender> autoDiscoverySender;
        private Mock<IAutoDiscoveryListener> autoDiscoveryListener;
        private Mock<IHeartBeatSenderConfigurationProvider> heartBeatSenderConfigProvider;
        private Uri heartBeatUri;
        private TimeSpan heartBeatInterval;

        [SetUp]
        public void Setup()
        {
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            logger = new Mock<ILogger>();
            routeDiscovery = new Mock<IRouteDiscovery>();
            scaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            autoDiscoverySender = new Mock<IAutoDiscoverySender>();
            autoDiscoveryListener = new Mock<IAutoDiscoveryListener>();
            heartBeatSenderConfigProvider = new Mock<IHeartBeatSenderConfigurationProvider>();
            heartBeatUri = new Uri("tcp://127.0.0.1:890");
            heartBeatSenderConfigProvider.Setup(m => m.GetHeartBeatAddress()).Returns(heartBeatUri);
            heartBeatInterval = TimeSpan.FromSeconds(5);
            heartBeatSenderConfigProvider.Setup(m => m.GetHeartBeatInterval()).Returns(heartBeatInterval);
            clusterMonitor = new ClusterMonitor(scaleOutConfigurationProvider.Object,
                                                autoDiscoverySender.Object,
                                                autoDiscoveryListener.Object,
                                                heartBeatSenderConfigProvider.Object,
                                                routeDiscovery.Object,
                                                securityProvider.Object,
                                                logger.Object);
        }

        [Test]
        public void RegisterSelf_SendRegistrationMessageForEachReceiver()
        {
            var actorIdentifier = ReceiverIdentities.CreateForActor();
            var messageHubIdentifier = ReceiverIdentities.CreateForMessageHub();
            var registrations = new[]
                                {
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<SimpleMessage>(),
                                        Receiver = actorIdentifier
                                    },
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<ExceptionMessage>(),
                                        Receiver = actorIdentifier
                                    },
                                    new MessageRoute
                                    {
                                        Receiver = messageHubIdentifier
                                    }
                                };
            //
            clusterMonitor.RegisterSelf(registrations, domain);
            //
            Func<IMessage, bool> messageIsConsistent = msg =>
                                                       {
                                                           var payload = msg.GetPayload<RegisterExternalMessageRouteMessage>();

                                                           Assert.AreEqual(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
                                                           Assert.IsTrue(Unsafe.ArraysEqual(payload.NodeIdentity, scaleOutAddress.Identity));
                                                           Assert.AreEqual(payload.Health.Uri, heartBeatUri.ToSocketAddress());
                                                           Assert.AreEqual(payload.Health.HeartBeatInterval, heartBeatInterval);
                                                           var actorRoutes = payload.Routes
                                                                                    .First(r => Unsafe.ArraysEqual(r.ReceiverIdentity, actorIdentifier.Identity));
                                                           foreach (var registration in registrations.Where(r => r.Receiver == actorIdentifier))
                                                           {
                                                               Assert.IsTrue(actorRoutes.MessageContracts.Any(mc => Unsafe.ArraysEqual(mc.Identity, registration.Message.Identity)
                                                                                                                    && Unsafe.ArraysEqual(mc.Partition, registration.Message.Partition)
                                                                                                                    && mc.Version == registration.Message.Version));
                                                           }
                                                           var messageHub = payload.Routes
                                                                                   .First(r => Unsafe.ArraysEqual(r.ReceiverIdentity, messageHubIdentifier.Identity));
                                                           Assert.IsNull(messageHub.MessageContracts);

                                                           return true;
                                                       };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => messageIsConsistent(msg))), Times.Once);
        }

        [Test]
        public void RegisterSelf_SendOneRegistrationMessageForSpecifiedDomainButNotMessageDomain()
        {
            var actorIdentifier = ReceiverIdentities.CreateForActor();
            var registrations = new[]
                                {
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<SimpleMessage>(),
                                        Receiver = actorIdentifier
                                    },
                                    new MessageRoute
                                    {
                                        Message = MessageIdentifier.Create<ExceptionMessage>(),
                                        Receiver = actorIdentifier
                                    }
                                };
            var simpleMessageDomain = Guid.NewGuid().ToString();
            var exceptionMessageDomain = Guid.NewGuid().ToString();
            var allowedDomains = new[] {exceptionMessageDomain, simpleMessageDomain, Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<SimpleMessage>().Identity)).Returns(simpleMessageDomain);
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<ExceptionMessage>().Identity)).Returns(exceptionMessageDomain);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            clusterMonitor.RegisterSelf(registrations, domain);
            //
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => msg.Domain == domain)), Times.Once);
        }

        [Test]
        public void UnregisterSelf_SendsOneUnregisterMessageRouteMessagePerDomain()
        {
            var actorIdentifier = ReceiverIdentities.CreateForActor();
            var messageHubIdentifier = ReceiverIdentities.CreateForMessageHub();
            var routes = new[]
                         {
                             new MessageRoute
                             {
                                 Message = MessageIdentifier.Create<SimpleMessage>(),
                                 Receiver = actorIdentifier
                             },
                             new MessageRoute
                             {
                                 Message = MessageIdentifier.Create<ExceptionMessage>(),
                                 Receiver = actorIdentifier
                             },
                             new MessageRoute
                             {
                                 Receiver = messageHubIdentifier
                             }
                         };
            var simpleMessageDomain = Guid.NewGuid().ToString();
            var exceptionMessageDomain = Guid.NewGuid().ToString();
            var allowedDomains = new[] {exceptionMessageDomain, simpleMessageDomain, Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<SimpleMessage>().Identity)).Returns(simpleMessageDomain);
            securityProvider.Setup(m => m.GetDomain(MessageIdentifier.Create<ExceptionMessage>().Identity)).Returns(exceptionMessageDomain);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            clusterMonitor.UnregisterSelf(routes);
            //
            Func<IMessage, bool> messageIsConsistent = msg =>
                                                       {
                                                           var payload = msg.GetPayload<UnregisterMessageRouteMessage>();

                                                           Assert.AreEqual(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
                                                           Assert.IsTrue(Unsafe.ArraysEqual(payload.ReceiverNodeIdentity, scaleOutAddress.Identity));
                                                           Assert.IsTrue(allowedDomains.Contains(msg.Domain));

                                                           return true;
                                                       };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => messageIsConsistent(msg))), Times.Exactly(allowedDomains.Length));
        }

        [Test]
        public void UnregisterSelfForMessagesWithoutReceiver_GroupsMessagesForSendingWithoutException()
        {
            var unregRoutes = new[]
                              {
                                  new MessageRoute
                                  {
                                      Message = MessageIdentifier.Create<SimpleMessage>(),
                                      Receiver = null
                                  },
                                  new MessageRoute
                                  {
                                      Message = MessageIdentifier.Create<ExceptionMessage>(),
                                      Receiver = null
                                  },
                                  new MessageRoute
                                  {
                                      Message = MessageIdentifier.Create<AsyncExceptionMessage>(),
                                      Receiver = null
                                  }
                              };
            //
            clusterMonitor.UnregisterSelf(unregRoutes);
            //
            Func<IMessage, bool> messageIsConsistent = msg =>
                                                       {
                                                           var payload = msg.GetPayload<UnregisterMessageRouteMessage>();

                                                           Assert.AreEqual(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
                                                           Assert.IsTrue(Unsafe.ArraysEqual(payload.ReceiverNodeIdentity, scaleOutAddress.Identity));
                                                           Assert.AreEqual(domain, msg.Domain);
                                                           Assert.AreEqual(1, payload.Routes.Length);
                                                           Assert.AreEqual(payload.Routes.First().MessageContracts.Length, unregRoutes.Length);
                                                           Assert.IsTrue(payload.Routes.All(r => r.ReceiverIdentity == null));

                                                           return true;
                                                       };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => messageIsConsistent(msg))), Times.Once);
        }
    }
}