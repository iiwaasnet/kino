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
using Xunit;

namespace kino.Tests.Cluster
{
    public class RouteDiscoveryTests
    {
        private readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(300);
        private readonly Mock<IAutoDiscoverySender> autoDiscoverySender;
        private readonly Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly Mock<ILogger> logger;
        private readonly ClusterMembershipConfiguration config;
        private readonly RouteDiscovery routeDiscovery;
        private readonly SocketEndpoint scaleOutAddress;
        private readonly string domain;

        public RouteDiscoveryTests()
        {
            autoDiscoverySender = new Mock<IAutoDiscoverySender>();
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutAddress = new SocketEndpoint("tcp://127.0.0.1:9090");
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            config = new ClusterMembershipConfiguration
                     {
                         RouteDiscovery = new RouteDiscoveryConfiguration
                                          {
                                              ClusterAutoDiscoveryStartDelay = TimeSpan.FromSeconds(1),
                                              ClusterAutoDiscoveryStartDelayMaxMultiplier = 2,
                                              MaxAutoDiscoverySenderQueueLength = 100,
                                              MissingRoutesDiscoverySendingPeriod = TimeSpan.FromSeconds(5),
                                              MaxMissingRouteDiscoveryRequestQueueLength = 100,
                                              MissingRoutesDiscoveryRequestsPerSend = 10
                                          }
                     };
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

        [Fact]
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
                                                          Assert.Null(payload.ReceiverIdentity);
                                                          Assert.True(Unsafe.ArraysEqual(payload.MessageContract.Identity, messageIdentifier.Identity));
                                                          Assert.True(Unsafe.ArraysEqual(payload.MessageContract.Partition, messageIdentifier.Partition));
                                                          Assert.Equal(payload.MessageContract.Version, messageIdentifier.Version);
                                                          Assert.True(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.Equal(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Once);
        }

        [Fact]
        public void IfSameMessageRouteRequestedAfterOthersAreSentButBeforeSendingPeriodEnds_TheyAreDeletedAndNotSentAgain()
        {
            config.RouteDiscovery = new RouteDiscoveryConfiguration {MissingRoutesDiscoverySendingPeriod = TimeSpan.FromSeconds(1)};
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
            config.RouteDiscovery.MissingRoutesDiscoverySendingPeriod.DivideBy(2).Sleep();
            for (var i = 0; i < Randomizer.Int32(5, 15); i++)
            {
                routeDiscovery.RequestRouteDiscovery(new MessageRoute
                                                     {
                                                         Message = messageIdentifier,
                                                         Receiver = receiverIdentifier
                                                     });
            }
            config.RouteDiscovery.MissingRoutesDiscoverySendingPeriod.Sleep();
            routeDiscovery.Stop();
            //
            Func<IMessage, bool> isDiscoveryMessage = msg =>
                                                      {
                                                          var payload = msg.GetPayload<DiscoverMessageRouteMessage>();
                                                          Assert.Null(payload.ReceiverIdentity);
                                                          Assert.True(Unsafe.ArraysEqual(payload.MessageContract.Identity, messageIdentifier.Identity));
                                                          Assert.True(Unsafe.ArraysEqual(payload.MessageContract.Partition, messageIdentifier.Partition));
                                                          Assert.Equal(payload.MessageContract.Version, messageIdentifier.Version);
                                                          Assert.True(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.Equal(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Once);
        }

        [Fact]
        public void MessageHubRouteDiscovery_IsSentForAllAllowedDomains()
        {
            var receiverIdentifier = ReceiverIdentities.CreateForMessageHub();
            var allowedDomains = Randomizer.Int32(2, 5).Produce(() => Guid.NewGuid().ToString());
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
                                                          Assert.Contains(msg.Domain, allowedDomains);
                                                          Assert.True(Unsafe.ArraysEqual(receiverIdentifier.Identity, payload.ReceiverIdentity));
                                                          Assert.Null(payload.MessageContract);
                                                          Assert.True(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.Equal(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Exactly(allowedDomains.Count()));
        }

        [Fact]
        public void IfSecurityExceptionThrownForOneMessageRoute_OthersAreStillSent()
        {
            var messageHub = ReceiverIdentities.CreateForMessageHub();
            var allowedDomains = Randomizer.Int32(2, 5).Produce(() => Guid.NewGuid().ToString());
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
                                                          Assert.Contains(msg.Domain, allowedDomains);
                                                          Assert.True(Unsafe.ArraysEqual(messageHub.Identity, payload.ReceiverIdentity));
                                                          Assert.Null(payload.MessageContract);
                                                          Assert.True(Unsafe.ArraysEqual(payload.RequestorNodeIdentity, scaleOutAddress.Identity));
                                                          Assert.Equal(payload.RequestorUri, scaleOutAddress.Uri.ToSocketAddress());
                                                          return true;
                                                      };
            autoDiscoverySender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => isDiscoveryMessage(msg))), Times.Exactly(allowedDomains.Count()));
            logger.Verify(m => m.Error(It.Is<object>(exc => exc is SecurityException)), Times.Once);
        }
    }
}