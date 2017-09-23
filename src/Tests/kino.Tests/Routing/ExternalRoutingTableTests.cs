using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using kino.Cluster;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Routing;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using Xunit;
using MessageRoute = kino.Routing.MessageRoute;

namespace kino.Tests.Routing
{
    public class ExternalRoutingTableTests
    {
        private readonly ExternalRoutingTable externalRoutingTable;
        private readonly Mock<ILogger> logger;

        public ExternalRoutingTableTests()
        {
            logger = new Mock<ILogger>();
            externalRoutingTable = new ExternalRoutingTable(logger.Object);
        }

        [Fact]
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
            Assert.Equal(routeRegistration.Peer, route.Node);
            Assert.Equal(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.Equal(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.False(route.Connected);
        }

        [Fact]
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
            Assert.Equal(routeRegistration.Peer, route.Node);
            Assert.Equal(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.Equal(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.False(route.Connected);
        }

        [Fact]
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
            Assert.Equal(routeRegistration.Peer, route.Node);
            Assert.Equal(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.Equal(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.False(route.Connected);
        }

        [Fact]
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
            Assert.Equal(routeRegistration.Peer, route.Node);
            Assert.Equal(routeRegistration.Health.Uri, route.Health.Uri);
            Assert.Equal(routeRegistration.Health.HeartBeatInterval, route.Health.HeartBeatInterval);
            Assert.False(route.Connected);
        }

        [Fact]
        public void IfNoRoutesRegisteredForMessage_EmptyListOfPeerConnectionsReturned()
        {
            var routeRegistration = CreateActorRouteRegistration(MessageIdentifier.Create<SimpleMessage>());
            var messageIdentifier = MessageIdentifier.Create<AsyncExceptionMessage>();
            externalRoutingTable.AddMessageRoute(routeRegistration);
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 Message = messageIdentifier,
                                                 ReceiverNodeIdentity = null
                                             };
            var routes = externalRoutingTable.FindRoutes(externalRouteLookupRequest);
            //
            Assert.Empty(routes);
        }

        [Fact]
        public void FindRoutesByMessageSentBroadcast_ReturnsAllRegisteredRoutes()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var routeRegistrations = Randomizer.Int32(3, 10)
                                               .Produce(() => CreateActorRouteRegistration(messageIdentifier));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 Message = messageIdentifier,
                                                 Distribution = DistributionPattern.Broadcast
                                             };
            var routes = externalRoutingTable.FindRoutes(externalRouteLookupRequest);
            //
            Assert.Equal(routeRegistrations.Count(), routes.Count());
        }

        [Fact]
        public void FindRoutesByMessageSentUnicast_ReturnsOneRoutes()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var routeRegistrations = Randomizer.Int32(3, 10)
                                               .Produce(() => CreateActorRouteRegistration(messageIdentifier));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 Message = messageIdentifier,
                                                 Distribution = DistributionPattern.Unicast
                                             };
            externalRoutingTable.FindRoutes(externalRouteLookupRequest).Single();
        }

        [Fact]
        public void RemoveNodeRoute_RemovesAllNodeRoutes()
        {
            var nodeIdentity = Guid.NewGuid().ToByteArray();
            var routeRegistrations = Randomizer.Int32(3, 10)
                                               .Produce(() =>
                                                        {
                                                            var messageIdentifier = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                          Randomizer.UInt16(),
                                                                                                          Guid.NewGuid().ToByteArray());
                                                            return CreateActorRouteRegistration(messageIdentifier,
                                                                                                ReceiverIdentities.CreateForActor(),
                                                                                                nodeIdentity);
                                                        });
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            externalRoutingTable.RemoveNodeRoute(new ReceiverIdentifier(nodeIdentity));
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 ReceiverNodeIdentity = new ReceiverIdentifier(nodeIdentity)
                                             };
            Assert.Empty(externalRoutingTable.FindRoutes(externalRouteLookupRequest));
        }

        [Fact]
        public void RemoveNodeRoute_KeepsConnectionIfMoreThanOneNodeRegisteredForSameUri()
        {
            var uri = "tcp://127.0.0.1:9009";
            var routeRegistrations = Randomizer.Int32(3, 10)
                                               .Produce(CreateActorRouteRegistration);
            routeRegistrations.ForEach(r => r.Peer = new Node(new Uri(uri), r.Peer.SocketIdentity));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            var nodeIdentity = routeRegistrations.First().Peer.SocketIdentity;
            //
            var peerConnection = externalRoutingTable.RemoveNodeRoute(new ReceiverIdentifier(nodeIdentity));
            //
            Assert.Equal(PeerConnectionAction.KeepConnection, peerConnection.ConnectionAction);
        }

        [Fact]
        public void RemoveMessageRouteForMessageHub_RemovesOneMessageHubAndKeepsPeerConnection()
        {
            var node = new Node("tcp://127.0.0.2:8080", Guid.NewGuid().ToByteArray());
            var health = new Health
                         {
                             Uri = "tcp://192.168.0.1:9090",
                             HeartBeatInterval = TimeSpan.FromSeconds(4)
                         };
            var messageHubRegistrations = Randomizer.Int32(2, 5)
                                                    .Produce(() => new ExternalRouteRegistration
                                                                   {
                                                                       Peer = node,
                                                                       Health = health,
                                                                       Route = new MessageRoute
                                                                               {
                                                                                   Receiver = ReceiverIdentities.CreateForMessageHub()
                                                                               }
                                                                   });
            messageHubRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            var messageHubToRemove = messageHubRegistrations.First().Route.Receiver;
            //
            var res = externalRoutingTable.RemoveMessageRoute(new ExternalRouteRemoval
                                                              {
                                                                  NodeIdentifier = node.SocketIdentity,
                                                                  Route = new MessageRoute {Receiver = messageHubToRemove}
                                                              });
            //
            Assert.Equal(PeerConnectionAction.KeepConnection, res.ConnectionAction);
            var peer = externalRoutingTable.FindRoutes(new ExternalRouteLookupRequest
                                                       {
                                                           ReceiverNodeIdentity = new ReceiverIdentifier(node.SocketIdentity)
                                                       }).First();
            Assert.Equal(node, peer.Node);
        }

        [Fact]
        public void IfLastMessageHubRouteIsRemoved_PeerWillBeDisconnected()
        {
            var node = new Node("tcp://127.0.0.2:8080", Guid.NewGuid().ToByteArray());
            var health = new Health
                         {
                             Uri = "tcp://192.168.0.1:9090",
                             HeartBeatInterval = TimeSpan.FromSeconds(4)
                         };
            var messageHubRegistrations = Randomizer.Int32(2, 5)
                                                    .Produce(() => new ExternalRouteRegistration
                                                                   {
                                                                       Peer = node,
                                                                       Health = health,
                                                                       Route = new MessageRoute
                                                                               {
                                                                                   Receiver = ReceiverIdentities.CreateForMessageHub()
                                                                               }
                                                                   });
            messageHubRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            for (var i = 0; i < messageHubRegistrations.Count(); i++)
            {
                var messageHubToRemove = messageHubRegistrations.ElementAt(i).Route.Receiver;
                var res = externalRoutingTable.RemoveMessageRoute(new ExternalRouteRemoval
                                                                  {
                                                                      NodeIdentifier = node.SocketIdentity,
                                                                      Route = new MessageRoute {Receiver = messageHubToRemove}
                                                                  });
                if (LastMessageHubRemoved(i, messageHubRegistrations.Count()))
                {
                    Assert.Equal(PeerConnectionAction.Disconnect, res.ConnectionAction);
                }
                else
                {
                    Assert.Equal(PeerConnectionAction.KeepConnection, res.ConnectionAction);
                }
            }
            //
            var peers = externalRoutingTable.FindRoutes(new ExternalRouteLookupRequest
                                                        {
                                                            ReceiverNodeIdentity = new ReceiverIdentifier(node.SocketIdentity)
                                                        });
            Assert.Empty(peers);
        }

        [Fact]
        public void RemoveMessageRouteForNode_RemovesAllActorRegistrationsForThisMessageForThisNode()
        {
            var nodeIdentity = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var routeRegistrations = Randomizer.Int32(3, 10)
                                               .Produce(() => CreateActorRouteRegistration(messageIdentifier,
                                                                                           ReceiverIdentities.CreateForActor(),
                                                                                           nodeIdentity));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //
            var res = externalRoutingTable.RemoveMessageRoute(new ExternalRouteRemoval
                                                              {
                                                                  Route = new MessageRoute {Message = messageIdentifier},
                                                                  NodeIdentifier = nodeIdentity
                                                              });
            //
            Assert.Equal(PeerConnectionAction.Disconnect, res.ConnectionAction);
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 ReceiverNodeIdentity = new ReceiverIdentifier(nodeIdentity)
                                             };
            Assert.Empty(externalRoutingTable.FindRoutes(externalRouteLookupRequest));
        }

        [Fact]
        public void IfAllNodeMessageRoutesAreRemoved_PeerIsDisconnected()
        {
            var nodeIdentity = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var actors = Randomizer.Int32(3, 10)
                                   .Produce(ReceiverIdentities.CreateForActor)
                                   .ToList();
            var routeRegistrations = actors.Select(actor => CreateActorRouteRegistration(messageIdentifier,
                                                                                         actor,
                                                                                         nodeIdentity));
            routeRegistrations.ForEach(r => externalRoutingTable.AddMessageRoute(r));
            //

            for (var i = 0; i < actors.Count(); i++)
            {
                var externalRouteRemoval = new ExternalRouteRemoval
                                           {
                                               NodeIdentifier = nodeIdentity,
                                               Route = new MessageRoute
                                                       {
                                                           Message = messageIdentifier,
                                                           Receiver = actors.ElementAt(i)
                                                       }
                                           };
                var res = externalRoutingTable.RemoveMessageRoute(externalRouteRemoval);
                if (LastMessageRouteRemoved(i, actors.Count()))
                {
                    Assert.Equal(PeerConnectionAction.Disconnect, res.ConnectionAction);
                }
                else
                {
                    Assert.Equal(PeerConnectionAction.KeepConnection, res.ConnectionAction);
                }
            }
            //
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 ReceiverNodeIdentity = new ReceiverIdentifier(nodeIdentity)
                                             };
            Assert.Empty(externalRoutingTable.FindRoutes(externalRouteLookupRequest));
        }

        [Fact]
        public void IfNodeIdentifierIsNotProvided_PeerRemovalResultIsNotFound()
        {
            IfNodeIdentifierIsNotProvidedOrNotFound_PeerRemovalResultIsNotFound(null);
        }

        [Fact]
        public void IfNodeIdentifierIsNotFound_PeerRemovalResultIsNotFound()
        {
            IfNodeIdentifierIsNotProvidedOrNotFound_PeerRemovalResultIsNotFound(Guid.NewGuid().ToByteArray());
        }

        [Fact]
        public void GetAllRoutes_ReturnsAllRegisteredMessageHubs()
        {
            var health = new Health();
            var nodes = new[]
                        {
                            new Node("tcp://127.0.0.1:8080", ReceiverIdentifier.CreateIdentity()),
                            new Node("tcp://192.168.0.1:9191", ReceiverIdentifier.CreateIdentity())
                        };

            var registrations = nodes.SelectMany(n => MessageHubs().Select(mh => (Node: n, MessageHub: mh)))
                                     .Select(reg => new ExternalRouteRegistration
                                                    {
                                                        Health = health,
                                                        Peer = reg.Node,
                                                        Route = new MessageRoute {Receiver = reg.MessageHub}
                                                    })
                                     .ToList();

            registrations.ForEach(reg => externalRoutingTable.AddMessageRoute(reg));
            //
            var routes = externalRoutingTable.GetAllRoutes();
            //
            Assert.Equal(nodes.Count(), routes.Count());
            nodes.Select(n => n.Uri).Should().BeEquivalentTo(routes.Select(r => r.Node.Uri));
            nodes.Select(n => n.SocketIdentity).Should().BeEquivalentTo(routes.Select(r => r.Node.SocketIdentity));

            AssertNodesMessageHubsAreSame(nodes.First());
            AssertNodesMessageHubsAreSame(nodes.Second());

            IEnumerable<ReceiverIdentifier> MessageHubs()
                => Randomizer.Int32(2, 8)
                             .Produce(ReceiverIdentities.CreateForMessageHub);

            void AssertNodesMessageHubsAreSame(Node node)
                => registrations.Where(r => r.Peer.Equals(node)).Select(r => r.Route.Receiver)
                                .Should()
                                .BeEquivalentTo(routes.Where(r => r.Node.Equals(node)).SelectMany(r => r.MessageHubs).Select(mh => mh.MessageHub));
        }

        [Fact]
        public void GetAllRoutes_ReturnsAllRegisteredMessageRoutes()
        {
            var health = new Health();
            var nodes = new[]
                        {
                            new Node("tcp://127.0.0.1:8080", ReceiverIdentifier.CreateIdentity()),
                            new Node("tcp://192.168.0.1:9191", ReceiverIdentifier.CreateIdentity())
                        };

            var messages = new[]
                           {
                               MessageIdentifier.Create<SimpleMessage>(),
                               MessageIdentifier.Create<SimpleMessage>(ReceiverIdentifier.CreateIdentity()),
                               MessageIdentifier.Create<AsyncExceptionMessage>(),
                               MessageIdentifier.Create<AsyncMessage>(ReceiverIdentifier.CreateIdentity())
                           };

            var registrations = nodes.SelectMany(n => messages.Select(m => (Node: n, Message: m)))
                                     .SelectMany(nm => Actors().Select(a => (Node: nm.Node, Message: nm.Message, Actor: a)))
                                     .Select(reg => new ExternalRouteRegistration
                                                    {
                                                        Health = health,
                                                        Peer = reg.Node,
                                                        Route = new MessageRoute {Receiver = reg.Actor, Message = reg.Message}
                                                    })
                                     .ToList();

            registrations.ForEach(reg => externalRoutingTable.AddMessageRoute(reg));
            //
            var routes = externalRoutingTable.GetAllRoutes();
            //
            Assert.Equal(nodes.Count(), routes.Count());
            nodes.Select(n => n.Uri).Should().BeEquivalentTo(routes.Select(r => r.Node.Uri));
            nodes.Select(n => n.SocketIdentity).Should().BeEquivalentTo(routes.Select(r => r.Node.SocketIdentity));

            AssertMessageRoutesAreSame(nodes.First());
            AssertMessageRoutesAreSame(nodes.Second());

            void AssertMessageRoutesAreSame(Node node)
            {
                var messageRoutes = registrations.Where(r => r.Peer.Equals(node))
                                                 .GroupBy(r => r.Route.Message, r => r.Route.Receiver);
                messageRoutes.Select(mr => mr.Key)
                             .Should()
                             .BeEquivalentTo(routes.Where(r => r.Node.Equals(node)).SelectMany(r => r.MessageRoutes).Select(mr => mr.Message));
                foreach (var messageIdentifier in messageRoutes.Select(mr => mr.Key))
                {
                    messageRoutes.Where(mr => mr.Key == messageIdentifier).SelectMany(mr => mr)
                                 .Should()
                                 .BeEquivalentTo(routes.Where(r => r.Node.Equals(node))
                                                       .SelectMany(r => r.MessageRoutes)
                                                       .Where(mr => mr.Message == messageIdentifier)
                                                       .SelectMany(mr => mr.Actors));
                }
            }

            IEnumerable<ReceiverIdentifier> Actors()
                => Randomizer.Int32(1, 8)
                             .Produce(ReceiverIdentities.CreateForActor);
        }

        private void IfNodeIdentifierIsNotProvidedOrNotFound_PeerRemovalResultIsNotFound(byte[] removeNodeIdentity)
        {
            var nodeIdentity = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var routeRegistration = CreateActorRouteRegistration(messageIdentifier,
                                                                 ReceiverIdentities.CreateForActor(),
                                                                 nodeIdentity);
            externalRoutingTable.AddMessageRoute(routeRegistration);
            //
            var res = externalRoutingTable.RemoveMessageRoute(new ExternalRouteRemoval
                                                              {
                                                                  Route = new MessageRoute {Message = messageIdentifier},
                                                                  NodeIdentifier = removeNodeIdentity
                                                              });
            //
            Assert.Equal(PeerConnectionAction.NotFound, res.ConnectionAction);
            var externalRouteLookupRequest = new ExternalRouteLookupRequest
                                             {
                                                 ReceiverNodeIdentity = new ReceiverIdentifier(nodeIdentity)
                                             };
            Assert.NotEmpty(externalRoutingTable.FindRoutes(externalRouteLookupRequest));
        }

        private bool LastMessageRouteRemoved(int i, int count)
            => i == count - 1;

        private static bool LastMessageHubRemoved(int i, int count)
            => i == count - 1;

        private static ExternalRouteRegistration CreateActorRouteRegistration()
            => CreateActorRouteRegistration(MessageIdentifier.Create<SimpleMessage>(),
                                            ReceiverIdentities.CreateForActor(),
                                            Guid.NewGuid().ToByteArray());

        private static ExternalRouteRegistration CreateActorRouteRegistration(MessageIdentifier messageIdentifier)
            => CreateActorRouteRegistration(messageIdentifier,
                                            ReceiverIdentities.CreateForActor(),
                                            Guid.NewGuid().ToByteArray());

        private static ExternalRouteRegistration CreateActorRouteRegistration(MessageIdentifier messageIdentifier,
                                                                              ReceiverIdentifier receiverIdentifier,
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
                               Receiver = receiverIdentifier
                           }
               };
    }
}