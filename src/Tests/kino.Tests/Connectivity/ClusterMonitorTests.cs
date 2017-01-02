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
        public void RegisterSelf_SendRegistrationMessageToAutoDiscoverySender()
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

        //[Test]
        //public void UnregisterSelf_SendUnregisterMessageRouteMessageToRendezvous()
        //{
        //    try
        //    {
        //        clusterMonitor.Start(StartTimeout);

        //        var messageIdentifier = new MessageIdentifier(Message.CurrentVersion);
        //        clusterMonitor.UnregisterSelf(new[] {messageIdentifier});

        //        var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
        //        var message = socket.GetSentMessages().BlockingLast(AsyncOp);

        //        Assert.IsNotNull(message);
        //        Assert.IsTrue(message.Equals(KinoMessages.UnregisterMessageRoute));
        //        var payload = message.GetPayload<UnregisterMessageRouteMessage>();
        //        Assert.IsTrue(Equals(payload.SocketIdentity, scaleOutAddress.Identity));
        //        Assert.AreEqual(payload.Uri, scaleOutAddress.Uri.ToSocketAddress());
        //        Assert.IsTrue(payload.MessageContracts.Any(mc => Equals(mc.Identity, messageIdentifier.Identity)
        //                                                         && Equals(mc.Version, messageIdentifier.Version)
        //                                                         && Equals(mc.Partition, messageIdentifier.Partition)));
        //    }
        //    finally
        //    {
        //        clusterMonitor.Stop();
        //    }
        //}

        //[Test]
        //public void DiscoverMessageRoute_SendDiscoverMessageRouteMessageToRendezvous()
        //{
        //    clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(10);
        //    clusterMembershipConfiguration.PongSilenceBeforeRouteDeletion = TimeSpan.FromSeconds(10);
        //    clusterMembershipConfiguration.RouteDiscovery.SendingPeriod = TimeSpan.FromMilliseconds(10);

        //    var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
        //                                            clusterMembership.Object,
        //                                            clusterMessageSender,
        //                                            clusterMessageListener,
        //                                            new RouteDiscovery(clusterMessageSender,
        //                                                               routerConfigurationProvider.Object,
        //                                                               clusterMembershipConfiguration,
        //                                                               securityProvider.Object,
        //                                                               logger.Object),
        //                                            securityProvider.Object);
        //    try
        //    {
        //        clusterMonitor.Start(StartTimeout);

        //        var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, Guid.NewGuid().ToByteArray(), IdentityExtensions.Empty);
        //        clusterMonitor.DiscoverMessageRoute(messageIdentifier);

        //        var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
        //        var message = socket.GetSentMessages().BlockingLast(TimeSpan.FromSeconds(2));

        //        Assert.IsNotNull(message);
        //        Assert.IsTrue(message.Equals(KinoMessages.DiscoverMessageRoute));
        //        var payload = message.GetPayload<DiscoverMessageRouteMessage>();
        //        Assert.IsTrue(Equals(payload.RequestorSocketIdentity, scaleOutAddress.Identity));
        //        Assert.AreEqual(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
        //        Assert.IsTrue(Equals(payload.MessageContract.Identity, messageIdentifier.Identity));
        //        Assert.IsTrue(Equals(payload.MessageContract.Version, messageIdentifier.Version));
        //        Assert.IsTrue(Equals(payload.MessageContract.Partition, messageIdentifier.Partition));
        //    }
        //    finally
        //    {
        //        clusterMonitor.Stop();
        //    }
        //}

        //[Test]
        //public void UnregisterSelf_SendsUnregisterMessagesForAllIdentitiesGroupedByDomain()
        //{
        //    var messageHubs = new[]
        //                      {
        //                          new MessageIdentifier(Guid.NewGuid().ToByteArray()),
        //                          new MessageIdentifier(Guid.NewGuid().ToByteArray()),
        //                          new MessageIdentifier(Guid.NewGuid().ToByteArray())
        //                      };
        //    var messageHandlers = new[]
        //                          {
        //                              MessageIdentifier.Create<SimpleMessage>(),
        //                              MessageIdentifier.Create<AsyncMessage>()
        //                          };
        //    var allowedDomains = new[]
        //                         {
        //                             Guid.NewGuid().ToString(),
        //                             Guid.NewGuid().ToString(),
        //                             Guid.NewGuid().ToString(),
        //                             Guid.NewGuid().ToString()
        //                         };
        //    securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
        //    foreach (var domain in allowedDomains)
        //    {
        //        securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
        //    }
        //    securityProvider.Setup(m => m.GetDomain(messageHandlers.First().Identity)).Returns(allowedDomains.First());
        //    securityProvider.Setup(m => m.GetDomain(messageHandlers.Second().Identity)).Returns(allowedDomains.Second());
        //    var clusterMessageSender = new Mock<IClusterMessageSender>();
        //    var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
        //                                            clusterMembership.Object,
        //                                            clusterMessageSender.Object,
        //                                            clusterMessageListener,
        //                                            routeDiscovery.Object,
        //                                            securityProvider.Object);
        //    //
        //    clusterMonitor.UnregisterSelf(messageHandlers.Concat(messageHubs));
        //    //
        //    clusterMessageSender.Verify(m => m.EnqueueMessage(It.IsAny<IMessage>()), Times.Exactly(allowedDomains.Count()));
        //    foreach (var domain in allowedDomains)
        //    {
        //        clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => msg.Domain == domain)));
        //    }
        //}

        //[Test]
        //public void UnregisterSelf_SendsUnregistrationMessageForEachMessageHubAndDomain()
        //{
        //    var messageHubs = new[]
        //                      {
        //                          new MessageIdentifier(Guid.NewGuid().ToByteArray()),
        //                          new MessageIdentifier(Guid.NewGuid().ToByteArray()),
        //                          new MessageIdentifier(Guid.NewGuid().ToByteArray())
        //                      };
        //    var messageHandlers = new[]
        //                          {
        //                              MessageIdentifier.Create<SimpleMessage>(),
        //                              MessageIdentifier.Create<AsyncMessage>()
        //                          };
        //    var allowedDomains = new[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
        //    securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
        //    foreach (var domain in allowedDomains)
        //    {
        //        securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
        //    }
        //    securityProvider.Setup(m => m.GetDomain(messageHandlers.First().Identity)).Returns(allowedDomains.First());
        //    securityProvider.Setup(m => m.GetDomain(messageHandlers.Second().Identity)).Returns(allowedDomains.Second());
        //    var clusterMessageSender = new Mock<IClusterMessageSender>();
        //    var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
        //                                            clusterMembership.Object,
        //                                            clusterMessageSender.Object,
        //                                            clusterMessageListener,
        //                                            routeDiscovery.Object,
        //                                            securityProvider.Object);
        //    //
        //    clusterMonitor.UnregisterSelf(messageHandlers.Concat(messageHubs));
        //    //
        //    foreach (var domain in allowedDomains)
        //    {
        //        foreach (var messageHub in messageHubs)
        //        {
        //            clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => AreEqual(msg, domain, messageHub))));
        //        }
        //    }

        //    clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => AreEqual(msg, allowedDomains.First(), messageHandlers.First()))));
        //    clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => AreEqual(msg, allowedDomains.Second(), messageHandlers.Second()))));
        //}

        //private static bool AreEqual(IMessage msg, string domain, MessageIdentifier messageHub)
        //{
        //    return msg.GetPayload<UnregisterMessageRouteMessage>()
        //              .MessageContracts
        //              .Any(mc => Equals(mc.Identity, messageHub.Identity))
        //           && msg.Domain == domain;
        //}
    }
}