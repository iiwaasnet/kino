using System;
using System.Linq;
using kino.Cluster;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Routing;
using kino.Tests.Actors.Setup;
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
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
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
                                                    Message = messageIdentifier,
                                                    Receiver = ReceiverIdentities.CreateForActor()
                                                }
                                    };
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
    }
}