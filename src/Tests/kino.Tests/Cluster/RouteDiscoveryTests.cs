using System;
using System.Linq;
using System.Security;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    [TestFixture]
    public class RouteDiscoveryTests
    {
        private readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(300);
        private Mock<IAutoDiscoverySender> autoDiscoverySender;
        private Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ILogger> logger;
        private ClusterMembershipConfiguration config;
        private RouteDiscovery routeDiscovery;
        private SocketEndpoint scaleOutAddress;
        private string domain;

        [SetUp]
        public void Setup()
        {
            autoDiscoverySender = new Mock<IAutoDiscoverySender>();
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutAddress = new SocketEndpoint("tcp://127.0.0.1:9090");
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            config = new ClusterMembershipConfiguration();
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            logger = new Mock<ILogger>();
            routeDiscovery = new RouteDiscovery(autoDiscoverySender.Object,
                                                scaleOutConfigurationProvider.Object,
                                                config,
                                                securityProvider.Object,
                                                logger.Object);
        }

        [Test]
        public void IfSameMessageRouteRequestedMultipleTimes_MessageForThatRouteIsSentOnlyOnce()
        {
            var receiverIdentifier = ReceiverIdentities.CreateForActor();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            //
            for (var i = 0; i < Randomizer.Int32(5, 15); i++)
            {
                routeDiscovery.RequestRouteDiscovery(new MessageRoute
                                                     {
                                                         Message = messageIdentifier,
                                                         Receiver = receiverIdentifier
                                                     });
            }
            routeDiscovery.Start();
            AsyncOp.Sleep();
            routeDiscovery.Stop();
            //
            Func<IMessage, bool> isDiscoveryMessage = msg =>
                                                      {
                                                          var payload = msg.GetPayload<DiscoverMessageRouteMessage>();
                                                          Assert.IsNull(payload.ReceiverIdentity);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.MessageContract.Identity, messageIdentifier.Identity));
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.MessageContract.Partition, messageIdentifier.Partition));
                                                          Assert.AreEqual(payload.MessageContract.Version, messageIdentifier.Version);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.AreEqual(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Once);
        }

        [Test]
        public void IfSameMessageRouteRequestedAfterOthersAreSentButBeforeSendingPeriodEnds_TheyAreDeletedAndNotSentAgain()
        {
            config.RouteDiscovery = new RouteDiscoveryConfiguration {SendingPeriod = TimeSpan.FromSeconds(1)};
            var receiverIdentifier = ReceiverIdentities.CreateForActor();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            //
            for (var i = 0; i < Randomizer.Int32(5, 15); i++)
            {
                routeDiscovery.RequestRouteDiscovery(new MessageRoute
                                                     {
                                                         Message = messageIdentifier,
                                                         Receiver = receiverIdentifier
                                                     });
            }
            routeDiscovery.Start();
            config.RouteDiscovery.SendingPeriod.DivideBy(2).Sleep();
            for (var i = 0; i < Randomizer.Int32(5, 15); i++)
            {
                routeDiscovery.RequestRouteDiscovery(new MessageRoute
                                                     {
                                                         Message = messageIdentifier,
                                                         Receiver = receiverIdentifier
                                                     });
            }
            config.RouteDiscovery.SendingPeriod.Sleep();
            routeDiscovery.Stop();
            //
            Func<IMessage, bool> isDiscoveryMessage = msg =>
                                                      {
                                                          var payload = msg.GetPayload<DiscoverMessageRouteMessage>();
                                                          Assert.IsNull(payload.ReceiverIdentity);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.MessageContract.Identity, messageIdentifier.Identity));
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.MessageContract.Partition, messageIdentifier.Partition));
                                                          Assert.AreEqual(payload.MessageContract.Version, messageIdentifier.Version);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.AreEqual(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Once);
        }

        [Test]
        public void MessageHubRouteDiscovery_IsSentForAllAllowedDomains()
        {
            var receiverIdentifier = ReceiverIdentities.CreateForMessageHub();
            var allowedDomains = EnumerableExtenions.Produce(Randomizer.Int32(2, 5),
                                                             () => Guid.NewGuid().ToString());
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            routeDiscovery.RequestRouteDiscovery(new MessageRoute {Receiver = receiverIdentifier});
            routeDiscovery.Start();
            AsyncOp.Sleep();
            routeDiscovery.Stop();
            //
            Func<IMessage, bool> isDiscoveryMessage = msg =>
                                                      {
                                                          var payload = msg.GetPayload<DiscoverMessageRouteMessage>();
                                                          CollectionAssert.Contains(allowedDomains, msg.Domain);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(receiverIdentifier.Identity, payload.ReceiverIdentity));
                                                          Assert.IsNull(payload.MessageContract);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.AreEqual(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Exactly(allowedDomains.Count()));
        }

        [Test]
        public void IfSecurityExceptionThrownForOneMessageRoute_OthersAreStillSent()
        {
            var messageHub = ReceiverIdentities.CreateForMessageHub();
            var allowedDomains = EnumerableExtenions.Produce(Randomizer.Int32(2, 5),
                                                             () => Guid.NewGuid().ToString());
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Throws<SecurityException>();
            //
            routeDiscovery.RequestRouteDiscovery(new MessageRoute {Receiver = messageHub});
            routeDiscovery.RequestRouteDiscovery(new MessageRoute
                                                 {
                                                     Receiver = ReceiverIdentities.CreateForActor(),
                                                     Message = MessageIdentifier.Create<SimpleMessage>()
                                                 });
            routeDiscovery.Start();
            AsyncOp.Sleep();
            routeDiscovery.Stop();
            //
            Func<IMessage, bool> isDiscoveryMessage = msg =>
                                                      {
                                                          var payload = msg.GetPayload<DiscoverMessageRouteMessage>();
                                                          CollectionAssert.Contains(allowedDomains, msg.Domain);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(messageHub.Identity, payload.ReceiverIdentity));
                                                          Assert.IsNull(payload.MessageContract);
                                                          Assert.IsTrue(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.AreEqual(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Exactly(allowedDomains.Count()));
            logger.Verify(m => m.Error(It.Is<object>(exc => exc is SecurityException)), Times.Once);
        }
    }
}