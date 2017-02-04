using System;
using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Core;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;
using MessageRoute = kino.Cluster.MessageRoute;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    [TestFixture]
    public class NodeRoutesRegistrarTests
    {
        private NodeRoutesRegistrar registrar;
        private Mock<IClusterServices> clusterServices;
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<ISecurityProvider> securityProvider;
        private string domain;
        private Mock<IClusterMonitor> clusterMonitor;

        [SetUp]
        public void Setup()
        {
            clusterServices = new Mock<IClusterServices>();
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterServices.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            registrar = new NodeRoutesRegistrar(clusterServices.Object,
                                                internalRoutingTable.Object,
                                                securityProvider.Object);
        }

        [Test]
        public void RegisterOwnGlobalRoutes_RegisteresOnlyGlobalyRegisteredActors()
        {
            var actors = EnumerableExtenions.Produce(Randomizer.Int32(5, 15),
                                                     i => new ReceiverIdentifierRegistration(ReceiverIdentities.CreateForActor(), i % 2 == 0));
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var internalRoutes = new InternalRouting
                                 {
                                     Actors = new[]
                                              {
                                                  new MessageActorRoute
                                                  {
                                                      Message = messageIdentifier,
                                                      Actors = actors
                                                  }
                                              },
                                     MessageHubs = Enumerable.Empty<MessageHubRoute>()
                                 };
            internalRoutingTable.Setup(m => m.GetAllRoutes()).Returns(internalRoutes);
            var globalActors = internalRoutes.Actors
                                             .SelectMany(r => r.Actors.Where(a => !a.LocalRegistration))
                                             .Select(a => new ReceiverIdentifier(a.Identity));
            //
            registrar.RegisterOwnGlobalRoutes(domain);
            //
            Func<IEnumerable<MessageRoute>, bool> isGlobalMessageRoute = mrs =>
                                                                         {
                                                                             Assert.IsTrue(mrs.All(mr => mr.Message == messageIdentifier));
                                                                             CollectionAssert.AreEquivalent(globalActors, mrs.Select(mr => mr.Receiver));
                                                                             return true;
                                                                         };
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => isGlobalMessageRoute(mrs)), domain), Times.Once);
        }

        [Test]
        public void RoutesFromNotFromRequestedDomain_AreNotRegistered()
        {
            var receiverIdentifierRegistration = new ReceiverIdentifierRegistration(ReceiverIdentities.CreateForActor(), false);
            var min = 5;
            var internalRoutes = new InternalRouting
                                 {
                                     Actors = EnumerableExtenions.Produce(Randomizer.Int32(min, 15),
                                                                          () => new MessageActorRoute
                                                                                {
                                                                                    Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                                                                    Randomizer.UInt16(),
                                                                                                                    Guid.NewGuid().ToByteArray()),
                                                                                    Actors = new[] {receiverIdentifierRegistration}
                                                                                }),
                                     MessageHubs = Enumerable.Empty<MessageHubRoute>()
                                 };
            internalRoutingTable.Setup(m => m.GetAllRoutes()).Returns(internalRoutes);
            var otherDomainRoutes = internalRoutes.Actors.Take(min / 2).ToList();
            var allowedDomainRoutes = internalRoutes.Actors.Except(otherDomainRoutes);
            foreach (var route in otherDomainRoutes)
            {
                securityProvider.Setup(m => m.GetDomain(route.Message.Identity)).Returns(Guid.NewGuid().ToString);
            }
            //
            registrar.RegisterOwnGlobalRoutes(domain);
            //
            Func<IEnumerable<MessageRoute>, bool> areAllowedMessageRoutes = mrs =>
                                                                            {
                                                                                CollectionAssert.AreEquivalent(allowedDomainRoutes.Select(r => r.Message), mrs.Select(r => r.Message));
                                                                                var receiverIdentifiers = allowedDomainRoutes.SelectMany(r => r.Actors.Select(a => new ReceiverIdentifier(a.Identity)));
                                                                                CollectionAssert.AreEquivalent(receiverIdentifiers, mrs.Select(r => r.Receiver));
                                                                                return true;
                                                                            };
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => areAllowedMessageRoutes(mrs)), domain), Times.Once);
        }

        [Test]
        public void RegisterOwnGlobalRoutes_RegisteresOnlyGlobalyRegisteredMessageHubs()
        {
            var internalRoutes = new InternalRouting
                                 {
                                     Actors = Enumerable.Empty<MessageActorRoute>(),
                                     MessageHubs = EnumerableExtenions.Produce(Randomizer.Int32(5, 15),
                                                                               i => new MessageHubRoute
                                                                                    {
                                                                                        MessageHub = ReceiverIdentities.CreateForMessageHub(),
                                                                                        LocalRegistration = i % 2 == 0
                                                                                    })
                                 };
            internalRoutingTable.Setup(m => m.GetAllRoutes()).Returns(internalRoutes);
            var globalMessageHubs = internalRoutes.MessageHubs.Where(mh => !mh.LocalRegistration);
            //
            registrar.RegisterOwnGlobalRoutes(domain);
            //
            Func<IEnumerable<MessageRoute>, bool> isGlobalMessageHub = mrs =>
                                                                       {
                                                                           CollectionAssert.AreEquivalent(globalMessageHubs.Select(mh => mh.MessageHub),
                                                                                                          mrs.Select(mr => mr.Receiver));
                                                                           return true;
                                                                       };
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => isGlobalMessageHub(mrs)), domain), Times.Once);
        }

        [Test]
        public void RegisterOwnGlobalRoutes_RegisteresOnlyGlobalyRegisteredMessageHubsAndActors()
        {
            var actors = EnumerableExtenions.Produce(Randomizer.Int32(5, 15),
                                                     i => new ReceiverIdentifierRegistration(ReceiverIdentities.CreateForActor(), i % 2 == 0));
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var internalRoutes = new InternalRouting
                                 {
                                     Actors = new[]
                                              {
                                                  new MessageActorRoute
                                                  {
                                                      Message = messageIdentifier,
                                                      Actors = actors
                                                  }
                                              },
                                     MessageHubs = EnumerableExtenions.Produce(Randomizer.Int32(5, 15),
                                                                               i => new MessageHubRoute
                                                                                    {
                                                                                        MessageHub = ReceiverIdentities.CreateForMessageHub(),
                                                                                        LocalRegistration = i % 2 == 0
                                                                                    })
                                 };
            internalRoutingTable.Setup(m => m.GetAllRoutes()).Returns(internalRoutes);
            var globalMessageHubs = internalRoutes.MessageHubs.Where(mh => !mh.LocalRegistration);
            var globalActors = internalRoutes.Actors
                                             .SelectMany(r => r.Actors.Where(a => !a.LocalRegistration))
                                             .Select(a => new ReceiverIdentifier(a.Identity));
            //
            registrar.RegisterOwnGlobalRoutes(domain);
            //
            Func<IEnumerable<MessageRoute>, bool> isGlobalMessageHub = mrs =>
                                                                       {
                                                                           CollectionAssert.AreEquivalent(globalMessageHubs.Select(mh => mh.MessageHub)
                                                                                                                           .Concat(globalActors),
                                                                                                          mrs.Select(mr => mr.Receiver));
                                                                           return true;
                                                                       };
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(mrs => isGlobalMessageHub(mrs)), domain), Times.Once);
        }
    }
}