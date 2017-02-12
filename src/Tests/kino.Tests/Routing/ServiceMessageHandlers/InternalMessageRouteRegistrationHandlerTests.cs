using System;
using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;
using MessageRoute = kino.Cluster.MessageRoute;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    [TestFixture]
    public class InternalMessageRouteRegistrationHandlerTests
    {
        private InternalMessageRouteRegistrationHandler handler;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ILocalSendingSocket<IMessage>> destinationSocket;
        private string domain;

        [SetUp]
        public void Setup()
        {
            clusterMonitor = new Mock<IClusterMonitor>();
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            destinationSocket = new Mock<ILocalSendingSocket<IMessage>>();
            handler = new InternalMessageRouteRegistrationHandler(clusterMonitor.Object,
                                                                  internalRoutingTable.Object,
                                                                  securityProvider.Object);
        }

        [Test]
        public void IfReceiverIdentifierIsNeitherActorNorMessageHub_MessageRouteIsNotAdded()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray())
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<InternalRouteRegistration>()), Times.Never);
        }

        [Test]
        public void LocalyRegisteredMessageHub_IsRegisteredInLocalRoutingTableButNotInCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                        KeepRegistrationLocal = true,
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()),
                                  Times.Never);
        }

        [Test]
        public void GlobalyRegisteredMessageHub_IsRegisteredInLocalRoutingTableAndCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                        KeepRegistrationLocal = false,
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(routes => routes.Any(r => r.Receiver == routeRegistration.ReceiverIdentifier)),
                                                      domain),
                                  Times.Once);
        }

        [Test]
        public void GlobalyRegisteredMessageHub_IsRegisteredInClusterOncePerEachDomain()
        {
            var allowedDomains = EnumerableExtensions.Produce(Randomizer.Int32(2, 5),
                                                             () => Guid.NewGuid().ToString());
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForMessageHub(),
                                        KeepRegistrationLocal = false,
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(routes => routes.Any(r => r.Receiver == routeRegistration.ReceiverIdentifier)),
                                                      It.Is<string>(d => allowedDomains.Contains(d))),
                                  Times.Exactly(allowedDomains.Count()));
        }

        [Test]
        public void LocalyRegisteredMessageRoutes_AreRegisteredInLocalRoutingTableButNotInCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                        MessageContracts = EnumerableExtensions.Produce(Randomizer.Int32(2, 5),
                                                                                       () => new MessageContract
                                                                                             {
                                                                                                 Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                                 Randomizer.UInt16(),
                                                                                                                                 Guid.NewGuid().ToByteArray()),
                                                                                                 KeepRegistrationLocal = true
                                                                                             }),
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void OnlyGlobalyRegisteredMessageRoutes_AreRegisteredInLocalRoutingTableButAndCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                        MessageContracts = EnumerableExtensions.Produce(Randomizer.Int32(5, 15),
                                                                                       i => new MessageContract
                                                                                            {
                                                                                                Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                                Randomizer.UInt16(),
                                                                                                                                Guid.NewGuid().ToByteArray()),
                                                                                                KeepRegistrationLocal = i % 2 == 0
                                                                                            }),
                                        DestinationSocket = destinationSocket.Object
                                    };
            //
            handler.Handle(routeRegistration);
            //
            Func<IEnumerable<MessageRoute>, bool> areGlobalMessageRoutes = mrs =>
                                                                           {
                                                                               CollectionAssert.AreEquivalent(routeRegistration.MessageContracts
                                                                                                                               .Where(mc => !mc.KeepRegistrationLocal)
                                                                                                                               .Select(mc => mc.Message),
                                                                                                              mrs.Select(mr => mr.Message));
                                                                               return true;
                                                                           };
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => areGlobalMessageRoutes(mrs)), domain), Times.Once);
        }

        [Test]
        public void MessageRouteRegistrations_AreGroupedByDomainWhenRegisteredAtCluster()
        {
            var routeRegistration = new InternalRouteRegistration
                                    {
                                        ReceiverIdentifier = ReceiverIdentities.CreateForActor(),
                                        MessageContracts = EnumerableExtensions.Produce(Randomizer.Int32(5, 15),
                                                                                       i => new MessageContract
                                                                                            {
                                                                                                Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                                Randomizer.UInt16(),
                                                                                                                                Guid.NewGuid().ToByteArray())
                                                                                            }),
                                        DestinationSocket = destinationSocket.Object
                                    };
            var secondDomain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(routeRegistration.MessageContracts.First().Message.Identity)).Returns(secondDomain);
            var allowedDomains = new[] {domain, secondDomain};
            //
            handler.Handle(routeRegistration);
            //
            Func<IEnumerable<MessageRoute>, bool> areGlobalMessageRoutes = mrs =>
                                                                           {
                                                                               CollectionAssert.IsSupersetOf(routeRegistration.MessageContracts
                                                                                                                              .Select(mc => mc.Message),
                                                                                                             mrs.Select(mr => mr.Message));
                                                                               return true;
                                                                           };
            internalRoutingTable.Verify(m => m.AddMessageRoute(routeRegistration), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => areGlobalMessageRoutes(mrs)),
                                                      It.Is<string>(d => allowedDomains.Contains(d))),
                                  Times.Exactly(allowedDomains.Length));
        }
    }
}