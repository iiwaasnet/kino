using System;
using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;
using MessageContract = kino.Messaging.Messages.MessageContract;
using MessageRoute = kino.Cluster.MessageRoute;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    [TestFixture]
    public class MessageRouteDiscoveryHandlerTests
    {
        private MessageRouteDiscoveryHandler handler;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ILogger> logger;
        private string domain;
        private InternalRouting internalRoutes;

        [SetUp]
        public void Setup()
        {
            clusterMonitor = new Mock<IClusterMonitor>();
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            logger = new Mock<ILogger>();
            internalRoutes = GetInternalRoutes();
            internalRoutingTable.Setup(m => m.GetAllRoutes()).Returns(internalRoutes);
            handler = new MessageRouteDiscoveryHandler(clusterMonitor.Object,
                                                       internalRoutingTable.Object,
                                                       securityProvider.Object,
                                                       logger.Object);
        }

        [Test]
        public void IfDomainIsNotAllowed_RegisterSelfIsNotCalled()
        {
            var payload = new DiscoverMessageRouteMessage();
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(Guid.NewGuid().ToString());
            //
            handler.Handle(message, null);
            //
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void IfDiscoveryMessageRouteIsForNotRegisteredMessageHub_RegisterSelfIsNotCalled()
        {
            var payload = new DiscoverMessageRouteMessage {ReceiverIdentity = ReceiverIdentities.CreateForMessageHub().Identity};
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, null);
            //
            internalRoutingTable.Verify(m => m.GetAllRoutes(), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void IfDiscoveryMessageRouteIsForNotRegisteredMessage_RegisterSelfIsNotCalled()
        {
            var payload = new DiscoverMessageRouteMessage
                          {
                              MessageContract = new MessageContract
                                                {
                                                    Identity = Guid.NewGuid().ToByteArray(),
                                                    Partition = Guid.NewGuid().ToByteArray(),
                                                    Version = Randomizer.UInt16()
                                                }
                          };
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, null);
            //
            internalRoutingTable.Verify(m => m.GetAllRoutes(), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void IfDiscoveryMessageRouteIsForMessageWithLocalyRegisteredActor_RegisterSelfIsNotCalled()
        {
            var localMessageRoute = internalRoutes.Actors.SelectMany(r => r.Actors
                                                                           .Where(a => a.LocalRegistration)
                                                                           .Select(a => r.Message))
                                                  .First();
            var payload = new DiscoverMessageRouteMessage
                          {
                              MessageContract = new MessageContract
                                                {
                                                    Identity = localMessageRoute.Identity,
                                                    Partition = localMessageRoute.Partition,
                                                    Version = localMessageRoute.Version
                                                }
                          };
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, null);
            //
            internalRoutingTable.Verify(m => m.GetAllRoutes(), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void IfDiscoveryMessageRouteIsForMessageWithGlobalyRegisteredActors_RegisterSelfIsCalledOnceForEachActor()
        {
            var localMessageRoute = internalRoutes.Actors
                                                  .SelectMany(r => r.Actors
                                                                    .Where(a => !a.LocalRegistration)
                                                                    .Select(a => new MessageRoute
                                                                                 {
                                                                                     Receiver = a,
                                                                                     Message = r.Message
                                                                                 }))
                                                  .GroupBy(a => a.Message)
                                                  .First();
            var payload = new DiscoverMessageRouteMessage
                          {
                              MessageContract = new MessageContract
                                                {
                                                    Identity = localMessageRoute.Key.Identity,
                                                    Partition = localMessageRoute.Key.Partition,
                                                    Version = localMessageRoute.Key.Version
                                                }
                          };
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            //
            handler.Handle(message, null);
            //
            Func<IEnumerable<MessageRoute>, bool> isRegisteredMessageRoute = rts =>
                                                                             {
                                                                                 var route = rts.First();
                                                                                 Assert.AreEqual(localMessageRoute.Key, route.Message);
                                                                                 Assert.IsTrue(localMessageRoute.Any( a => a.Receiver == route.Receiver));
                                                                                 return true;
                                                                             };
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(rts => isRegisteredMessageRoute(rts)), domain), Times.Exactly(localMessageRoute.Count()));
        }

        [Test]
        public void IfDiscoveryMessageRouteDomainNotEqualsToActorMessageDomain_RegisterSelfIsNotCalled()
        {
            var localMessageRoute = internalRoutes.Actors
                                                  .SelectMany(r => r.Actors
                                                                    .Where(a => !a.LocalRegistration)
                                                                    .Select(a => new MessageRoute
                                                                                 {
                                                                                     Receiver = a,
                                                                                     Message = r.Message
                                                                                 }))
                                                  .First();
            var payload = new DiscoverMessageRouteMessage
                          {
                              MessageContract = new MessageContract
                                                {
                                                    Identity = localMessageRoute.Message.Identity,
                                                    Partition = localMessageRoute.Message.Partition,
                                                    Version = localMessageRoute.Message.Version
                                                }
                          };
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            securityProvider.Setup(m => m.GetDomain(localMessageRoute.Message.Identity)).Returns(Guid.NewGuid().ToString);
            //
            handler.Handle(message, null);
            //
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void IfDiscoveryMessageRouteIsForGlobalyRegisteredMessagHub_RegisterSelfIsCalled()
        {
            var localMessageRoute = internalRoutes.MessageHubs
                                                  .Where(mh => !mh.LocalRegistration)
                                                  .Select(mh => new MessageRoute
                                                                {
                                                                    Receiver = mh.MessageHub
                                                                })
                                                  .First();
            var payload = new DiscoverMessageRouteMessage {ReceiverIdentity = localMessageRoute.Receiver.Identity};
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            var allowedDomains = EnumerableExtenions.Produce(Randomizer.Int32(2, 5), () => Guid.NewGuid().ToString()).Concat(new[] {domain});
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            handler.Handle(message, null);
            //
            Func<IEnumerable<MessageRoute>, bool> isRegisteredMessageHub = rts =>
                                                                           {
                                                                               var route = rts.First();
                                                                               Assert.AreEqual(localMessageRoute.Receiver, route.Receiver);
                                                                               return true;
                                                                           };
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageRoute>>(rts => isRegisteredMessageHub(rts)), domain), Times.Once);
        }

        [Test]
        public void IfDiscoveryMessageRouteIsForLocalyRegisteredMessagHub_RegisterSelfIsCalled()
        {
            var localMessageRoute = internalRoutes.MessageHubs
                                                  .Where(mh => mh.LocalRegistration)
                                                  .Select(mh => new MessageRoute
                                                                {
                                                                    Receiver = mh.MessageHub
                                                                })
                                                  .First();
            var payload = new DiscoverMessageRouteMessage {ReceiverIdentity = localMessageRoute.Receiver.Identity};
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            var allowedDomains = EnumerableExtenions.Produce(Randomizer.Int32(2, 5), () => Guid.NewGuid().ToString()).Concat(new[] {domain});
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            //
            handler.Handle(message, null);
            //
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageRoute>>(), It.IsAny<string>()), Times.Never);
        }

        private InternalRouting GetInternalRoutes()
            => new InternalRouting
               {
                   Actors = EnumerableExtenions.Produce(Randomizer.Int32(2, 5),
                                                        () => new MessageActorRoute
                                                              {
                                                                  Actors = EnumerableExtenions.Produce(Randomizer.Int32(5, 15),
                                                                                                       i => new ReceiverIdentifierRegistration(ReceiverIdentities.CreateForActor(), i % 2 == 0)),
                                                                  Message = new MessageIdentifier(Guid.NewGuid().ToByteArray(), Randomizer.UInt16(), Guid.NewGuid().ToByteArray())
                                                              }),
                   MessageHubs = EnumerableExtenions.Produce(Randomizer.Int32(2, 5),
                                                             i => new MessageHubRoute
                                                                  {
                                                                      MessageHub = ReceiverIdentities.CreateForMessageHub(),
                                                                      LocalRegistration = i % 2 == 0
                                                                  })
               };
    }
}