using System;
using System.Linq;
using kino.Cluster;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Routing;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;
using MessageRoute = kino.Routing.MessageRoute;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ExternalRoutingTableTests
    {
        private ExternalRoutingTable externalRoutingTable;
        private Mock<ILogger> logger;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            externalRoutingTable = new ExternalRoutingTable(logger.Object);
        }

        [Test]
        public void AddMessageRoute_AddsActorRoute()
        {
            var routeRegistration = CreateActorRouteRegistration();
            var messageIdentifier = routeRegistration.Route.Message;
            //
            externalRoutingTable.AddMessageRoute(routeRegistration);
            var route = externalRoutingTable.FindRoutes(new ExternalRouteLookupRequest
                                                        {
                                                            Message = messageIdentifier
                                                        })
                                            .First();
            //
            Assert.AreEqual(routeRegistration.Peer, route.Node);
            Assert.AreEqual(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.AreEqual(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.IsFalse(route.Connected);
        }

        [Test]
        public void AddMessageRoute_AddsMessageHub()
        {
            var routeRegistration = new ExternalRouteRegistration
                                    {
                                        Peer = new Node("tcp://127.0.0.2:8080", Guid.NewGuid().ToByteArray()),
                                        Health = new Health
                                                 {
                                                     Uri = "tcp://192.168.0.1:9090",
                                                     HeartBeatInterval = TimeSpan.FromSeconds(4)
                                                 },
                                        Route = new MessageRoute
                                                {
                                                    Receiver = ReceiverIdentities.CreateForMessageHub()
                                                }
                                    };
            //
            externalRoutingTable.AddMessageRoute(routeRegistration);
            var route = externalRoutingTable.FindRoutes(new ExternalRouteLookupRequest
                                                        {
                                                            ReceiverIdentity = routeRegistration.Route.Receiver,
                                                            ReceiverNodeIdentity = new ReceiverIdentifier(routeRegistration.Peer.SocketIdentity)
                                                        })
                                            .First();
            //
            Assert.AreEqual(routeRegistration.Peer, route.Node);
            Assert.AreEqual(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.AreEqual(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.IsFalse(route.Connected);
        }

        [Test]
        public void FindRoutesByReceiverNode_ReturnsRouteRegardlessOfMessage()
        {
            var routeRegistration = CreateActorRouteRegistration();
            var receiverIdentity = new ReceiverIdentifier(routeRegistration.Peer.SocketIdentity);
            externalRoutingTable.AddMessageRoute(routeRegistration);
            //
            var route = externalRoutingTable.FindRoutes(new ExternalRouteLookupRequest
                                                        {
                                                            ReceiverNodeIdentity = receiverIdentity
                                                        })
                                            .First();
            //
            Assert.AreEqual(routeRegistration.Peer, route.Node);
            Assert.AreEqual(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.AreEqual(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.IsFalse(route.Connected);
        }

        [Test]
        public void FindRoutesByMessage_ReturnsRouteRegardlessOfReceiverNode()
        {
            var routeRegistration = CreateActorRouteRegistration();
            var messageIdentifier = routeRegistration.Route.Message;
            externalRoutingTable.AddMessageRoute(routeRegistration);
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 Message = messageIdentifier,
                                                 ReceiverNodeIdentity = null
                                             };
            var route = externalRoutingTable.FindRoutes(externalRouteLookupRequest)
                                            .First();
            //
            Assert.AreEqual(routeRegistration.Peer, route.Node);
            Assert.AreEqual(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.AreEqual(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.IsFalse(route.Connected);
        }

        [Test]
        public void FindRoutesByMessageSentBroadcast_ReturnsAllRegisteredRoutes()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var routeRegistrations = EnumerableExtenions.Produce(Randomizer.Int32(3, 10),
                                                                 () => CreateActorRouteRegistration(messageIdentifier));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 Message = messageIdentifier,
                                                 Distribution = DistributionPattern.Broadcast
                                             };
            var routes = externalRoutingTable.FindRoutes(externalRouteLookupRequest);
            //
            Assert.AreEqual(routeRegistrations.Count(), routes.Count());
        }

        [Test]
        public void FindRoutesByMessageSentUnicast_ReturnsOneRoutes()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var routeRegistrations = EnumerableExtenions.Produce(Randomizer.Int32(3, 10),
                                                                 () => CreateActorRouteRegistration(messageIdentifier));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 Message = messageIdentifier,
                                                 Distribution = DistributionPattern.Unicast
                                             };
            Assert.DoesNotThrow(() => externalRoutingTable.FindRoutes(externalRouteLookupRequest).Single());
        }

        [Test]
        public void RemoveNodeRoute_RemovesAllNodeRoutes()
        {
            var nodeIdentifier = Guid.NewGuid().ToByteArray();
            var routeRegistrations = EnumerableExtenions.Produce(Randomizer.Int32(3, 10),
                                                                 () => CreateActorRouteRegistration(new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                          Randomizer.UInt16(),
                                                                                                                          Guid.NewGuid().ToByteArray()),
                                                                                                    nodeIdentifier));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            externalRoutingTable.RemoveNodeRoute(new ReceiverIdentifier(nodeIdentifier));
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 ReceiverNodeIdentity = new ReceiverIdentifier(nodeIdentifier)
                                             };
            CollectionAssert.IsEmpty(externalRoutingTable.FindRoutes(externalRouteLookupRequest));
        }

        private static ExternalRouteRegistration CreateActorRouteRegistration()
            => CreateActorRouteRegistration(MessageIdentifier.Create<SimpleMessage>(), Guid.NewGuid().ToByteArray());

        private static ExternalRouteRegistration CreateActorRouteRegistration(MessageIdentifier messageIdentifier)
            => CreateActorRouteRegistration(messageIdentifier, Guid.NewGuid().ToByteArray());

        private static ExternalRouteRegistration CreateActorRouteRegistration(MessageIdentifier messageIdentifier,
                                                                              byte[] nodeIdentity)
            => new ExternalRouteRegistration
               {
                   Peer = new Node("tcp://127.0.0.2:8080", nodeIdentity),
                   Health = new Health
                            {
                                Uri = "tcp://192.168.0.1:9090",
                                HeartBeatInterval = TimeSpan.FromSeconds(4)
                            },
                   Route = new MessageRoute
                           {
                               Message = messageIdentifier,
                               Receiver = ReceiverIdentities.CreateForActor()
                           }
               };
    }
}