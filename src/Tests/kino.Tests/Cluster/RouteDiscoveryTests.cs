using System;
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
    }
}