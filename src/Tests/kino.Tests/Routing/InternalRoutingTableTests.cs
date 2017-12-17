using System;
using System.Linq;
using FluentAssertions;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Routing;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using Xunit;

namespace kino.Tests.Routing
{
    public class InternalRoutingTableTests
    {
        private readonly InternalRoutingTable internalRoutingTable;

        public InternalRoutingTableTests()
            => internalRoutingTable = new InternalRoutingTable(new RoundRobinDestinationList(new Mock<ILogger>().Object));

        [Fact]
        public void IfReceiverIdentifierNeitherMessageHubNorActor_AddMessageRouteThrowsException()
        {
            var receiverIdentity = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var localSocket = new LocalSocket<IMessage>();
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = receiverIdentity,
                                   DestinationSocket = localSocket
                               };
            //
            Assert.Throws<ArgumentException>(() => internalRoutingTable.AddMessageRoute(registration));
        }

        [Fact]
        public void AddMessageRoute_AddsMessageHubRoute()
        {
            var messageHub = ReceiverIdentities.CreateForMessageHub();
            var localSocket = new LocalSocket<IMessage>();
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = messageHub,
                                   DestinationSocket = localSocket
                               };
            //
            internalRoutingTable.AddMessageRoute(registration);
            //
            var lookupRequest = new InternalRouteLookupRequest
                                {
                                    ReceiverIdentity = messageHub
                                };
            var socket = internalRoutingTable.FindRoutes(lookupRequest).First();
            Assert.Equal(localSocket, socket);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddMessageRoute_AddsLocalOrExternalMessageHubRoute(bool keepLocal)
        {
            var messageHub = ReceiverIdentities.CreateForMessageHub();
            var localSocket = new LocalSocket<IMessage>();
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = messageHub,
                                   DestinationSocket = localSocket,
                                   KeepRegistrationLocal = keepLocal
                               };
            //
            internalRoutingTable.AddMessageRoute(registration);
            //
            var route = internalRoutingTable.GetAllRoutes().MessageHubs.First();
            Assert.Equal(keepLocal, route.LocalRegistration);
            Assert.Equal(messageHub, route.MessageHub);
        }

        [Fact]
        public void AddMessageRoute_AddsActorRoute()
        {
            var actor = ReceiverIdentities.CreateForActor();
            var localSocket = new LocalSocket<IMessage>();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = actor,
                                   DestinationSocket = localSocket,
                                   MessageContracts = new[] {new MessageContract {Message = messageIdentifier}}
                               };
            //
            internalRoutingTable.AddMessageRoute(registration);
            //
            var lookupRequest = new InternalRouteLookupRequest
                                {
                                    ReceiverIdentity = actor,
                                    Message = messageIdentifier
                                };
            var socket = internalRoutingTable.FindRoutes(lookupRequest).First();
            Assert.Equal(localSocket, socket);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddMessageRoute_AddsLocalOrExternalActorRoute(bool keepLocal)
        {
            var actor = ReceiverIdentities.CreateForActor();
            var localSocket = new LocalSocket<IMessage>();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = actor,
                                   DestinationSocket = localSocket,
                                   MessageContracts = new[]
                                                      {
                                                          new MessageContract
                                                          {
                                                              Message = messageIdentifier,
                                                              KeepRegistrationLocal = keepLocal
                                                          }
                                                      }
                               };
            //
            internalRoutingTable.AddMessageRoute(registration);
            //
            var route = internalRoutingTable.GetAllRoutes().Actors.First().Actors.First();
            Assert.Equal(keepLocal, route.LocalRegistration);
            Assert.True(Unsafe.ArraysEqual(actor.Identity, route.Identity));
        }

        [Fact]
        public void FindRoutesByMessageHubReceiverIdentity_ReturnsMessageHubSocket()
        {
            var registrations = Randomizer.Int32(3, 6)
                                          .Produce(() => new InternalRouteRegistration
                                                         {
                                                             ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                                             DestinationSocket = new LocalSocket<IMessage>()
                                                         })
                                          .ForEach(r => internalRoutingTable.AddMessageRoute(r))
                                          .ToList();
            var lookupRoute = registrations.First();
            var messageHub = lookupRoute.ReceiverIdentifier;
            var localSocket = lookupRoute.DestinationSocket;
            //
            var lookupRequest = new InternalRouteLookupRequest
                                {
                                    ReceiverIdentity = messageHub
                                };
            var socket = internalRoutingTable.FindRoutes(lookupRequest)
                                             .First();
            //
            Assert.Equal(localSocket, socket);
        }

        [Fact]
        public void FindRoutesByActorReceiverIdentifier_ReturnsActorHostSocket()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var registrations = Randomizer.Int32(3, 6)
                                          .Produce(() => new InternalRouteRegistration
                                                         {
                                                             ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                                             DestinationSocket = new LocalSocket<IMessage>(),
                                                             MessageContracts = new[] {new MessageContract {Message = messageIdentifier}}
                                                         })
                                          .ForEach(r => internalRoutingTable.AddMessageRoute(r))
                                          .ToList();
            var lookupRoute = registrations.First();
            var actor = lookupRoute.ReceiverIdentifier;
            var localSocket = lookupRoute.DestinationSocket;
            //
            var socket = internalRoutingTable.FindRoutes(new InternalRouteLookupRequest
                                                         {
                                                             Message = messageIdentifier,
                                                             ReceiverIdentity = actor
                                                         })
                                             .First();
            //
            Assert.Equal(localSocket, socket);
        }

        [Fact]
        public void IfSeveralActorsRegisteredToHandleTheMessage_TheAreFoundInRoundRobinManner()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var registrations = Randomizer.Int32(6, 16)
                                          .Produce(() => new InternalRouteRegistration
                                                         {
                                                             ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                                             DestinationSocket = new LocalSocket<IMessage>(),
                                                             MessageContracts = new[] {new MessageContract {Message = messageIdentifier}}
                                                         })
                                          .ForEach(r => internalRoutingTable.AddMessageRoute(r))
                                          .ToList();
            //
            foreach (var registration in registrations)
            {
                var socket = internalRoutingTable.FindRoutes(new InternalRouteLookupRequest
                                                             {
                                                                 Message = messageIdentifier
                                                             })
                                                 .First();
                var localSocket = registration.DestinationSocket;
                //
                Assert.Equal(localSocket, socket);
            }
        }

        [Fact]
        public void FindBroadcastMessage_ReturnsAllRegisteredActors()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var registrations = Randomizer.Int32(6, 16)
                                          .Produce(() => new InternalRouteRegistration
                                                         {
                                                             ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                                             DestinationSocket = new LocalSocket<IMessage>(),
                                                             MessageContracts = new[] {new MessageContract {Message = messageIdentifier}}
                                                         })
                                          .ForEach(r => internalRoutingTable.AddMessageRoute(r))
                                          .ToList();
            //
            var sockets = internalRoutingTable.FindRoutes(new InternalRouteLookupRequest
                                                          {
                                                              Message = messageIdentifier,
                                                              Distribution = DistributionPattern.Broadcast
                                                          });

            //
            registrations.Select(r => r.DestinationSocket)
                         .Should()
                         .BeEquivalentTo(sockets);
        }

        [Fact]
        public void IfMessageRouteIsNotRegistered_NoActorsReturned()
        {
            var registrations = Randomizer.Int32(6, 16)
                                          .Produce(() => new InternalRouteRegistration
                                                         {
                                                             ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                                             DestinationSocket = new LocalSocket<IMessage>(),
                                                             MessageContracts = new[] {new MessageContract {Message = MessageIdentifier.Create<SimpleMessage>()}}
                                                         });
            registrations.ForEach(r => internalRoutingTable.AddMessageRoute(r));
            //
            var sockets = internalRoutingTable.FindRoutes(new InternalRouteLookupRequest
                                                          {
                                                              Message = MessageIdentifier.Create<AsyncMessage>()
                                                          });

            //
            Assert.Empty(sockets);
        }

        [Fact]
        public void RemoveReceiverRoute_RemoveAllActorRegistrations()
        {
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                   DestinationSocket = new LocalSocket<IMessage>(),
                                   MessageContracts = Randomizer.Int32(4, 14)
                                                                .Produce(() => new MessageContract
                                                                               {
                                                                                   Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                   Randomizer.UInt16(),
                                                                                                                   Guid.NewGuid().ToByteArray())
                                                                               })
                               };
            internalRoutingTable.AddMessageRoute(registration);
            //
            var routes = internalRoutingTable.RemoveReceiverRoute(registration.DestinationSocket);
            //
            registration.MessageContracts.Select(mc => mc.Message)
                        .Should()
                        .BeEquivalentTo(routes.Select(r => r.Message));
            Assert.Empty(internalRoutingTable.GetAllRoutes().Actors);
        }

        [Fact]
        public void RemoveReceiverRoute_RemoveAllMessageHubRegistrations()
        {
            var registration = new InternalRouteRegistration
                               {
                                   ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                   DestinationSocket = new LocalSocket<IMessage>()
                               };
            internalRoutingTable.AddMessageRoute(registration);
            //
            var route = internalRoutingTable.RemoveReceiverRoute(registration.DestinationSocket)
                                            .First();
            //
            Assert.Equal(registration.ReceiverIdentifier, route.Receiver);
        }
    }
}