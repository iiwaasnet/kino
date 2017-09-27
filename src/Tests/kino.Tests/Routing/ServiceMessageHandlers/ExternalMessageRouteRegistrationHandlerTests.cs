using System;
using System.Linq;
using System.Security;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Health = kino.Messaging.Messages.Health;
using MessageContract = kino.Messaging.Messages.MessageContract;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class ExternalMessageRouteRegistrationHandlerTests
    {
        private readonly Mock<ILogger> logger;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly string domain;
        private readonly Mock<IExternalRoutingTable> externalRoutingTable;
        private readonly ExternalMessageRouteRegistrationHandler handler;
        private readonly Mock<ISocket> socket;
        private readonly Mock<IClusterHealthMonitor> clusterHealthMonitor;

        public ExternalMessageRouteRegistrationHandlerTests()
        {
            logger = new Mock<ILogger>();
            socket = new Mock<ISocket>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<ExternalRouteRegistration>()))
                                .Returns(new PeerConnection {Connected = false});

            domain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.DomainIsAllowed(It.Is<string>(d => d != domain))).Returns(false);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable.Object,
                                                                  securityProvider.Object,
                                                                  clusterHealthMonitor.Object,
                                                                  logger.Object);
        }

        [Fact]
        public void PeerAddedToClusterHealthMonitor_OnlyOnce()
        {
            var payload = CreateRegisterExternalMessageRoutePayload();
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, socket.Object);
            //
            Func<Node, bool> isThisPeer = p => p.Uri.ToSocketAddress() == payload.Uri
                                               && Unsafe.ArraysEqual(p.SocketIdentity, payload.NodeIdentity);
            clusterHealthMonitor.Verify(m => m.AddPeer(It.Is<Node>(p => isThisPeer(p)), It.IsAny<kino.Cluster.Health>()), Times.Once);
        }

        [Fact]
        public void IfDomainIsAllowed_AllRoutesAreAdded()
        {
            var payload = CreateRegisterExternalMessageRoutePayload();
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            var actorRoutes = payload.Routes.First(r => r.ReceiverIdentity.IsActor());
            var messageHub = payload.Routes.First(r => r.ReceiverIdentity.IsMessageHub());
            //
            handler.Handle(message, socket.Object);
            //
            Func<ExternalRouteRegistration,
                MessageIdentifier,
                bool> allActorRoutes = (er, messageIdentifer) => er.Route.Receiver.IsActor()
                                                                 && Unsafe.ArraysEqual(er.Route.Receiver.Identity, actorRoutes.ReceiverIdentity)
                                                                 && er.Route.Message == messageIdentifer;
            Func<ExternalRouteRegistration, bool> thisMessageHub = er => Unsafe.ArraysEqual(er.Route.Receiver.Identity, messageHub.ReceiverIdentity);

            foreach (var messageIdentifier in actorRoutes.MessageContracts
                                                         .Select(mc => new MessageIdentifier(mc.Identity, mc.Version, mc.Partition)))
            {
                externalRoutingTable.Verify(m => m.AddMessageRoute(It.Is<ExternalRouteRegistration>(er => allActorRoutes(er, messageIdentifier))), Times.Once);
            }

            externalRoutingTable.Verify(m => m.AddMessageRoute(It.Is<ExternalRouteRegistration>(er => thisMessageHub(er))), Times.Once);
        }

        [Fact]
        public void IfRegistrationDomainIsNotAllowed_RoutesAreNotAdded()
        {
            var payload = CreateRegisterExternalMessageRoutePayload();
            var message = Message.Create(payload).As<Message>();
            var notAllowedDomain = Guid.NewGuid().ToString();
            message.SetDomain(notAllowedDomain);
            //
            handler.Handle(message, socket.Object);
            //
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<ExternalRouteRegistration>()), Times.Never);
            clusterHealthMonitor.Verify(m => m.AddPeer(It.IsAny<Node>(), It.IsAny<kino.Cluster.Health>()), Times.Never);
        }

        [Fact]
        public void IfMessageDomainIsNotEqualToRegistrationDomain_MessageRouteIsNotAdded()
        {
            var payload = CreateRegisterExternalMessageRoutePayload();
            var message = Message.Create(payload).As<Message>();
            var notAllowedMessage = payload.Routes.First(r => r.ReceiverIdentity.IsActor()).MessageContracts.First();
            securityProvider.Setup(m => m.GetDomain(notAllowedMessage.Identity)).Returns(Guid.NewGuid().ToString);
            message.SetDomain(domain);
            //
            handler.Handle(message, socket.Object);
            //
            Func<ExternalRouteRegistration, bool> isNotAllowedMessage = er => er.Route.Receiver.IsActor()
                                                                              && Unsafe.ArraysEqual(er.Route.Message.Identity, notAllowedMessage.Identity)
                                                                              && er.Route.Message.Version == notAllowedMessage.Version
                                                                              && Unsafe.ArraysEqual(er.Route.Message.Partition, notAllowedMessage.Partition);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.Is<ExternalRouteRegistration>(er => isNotAllowedMessage(er))), Times.Never);
        }

        private static RegisterExternalMessageRouteMessage CreateRegisterExternalMessageRoutePayload()
            => new RegisterExternalMessageRouteMessage
               {
                   Uri = "tcp://127.0.0.1:80",
                   NodeIdentity = Guid.NewGuid().ToByteArray(),
                   Health = new Health
                            {
                                Uri = "tcp://127.0.0.1:812",
                                HeartBeatInterval = TimeSpan.FromSeconds(4)
                            },
                   Routes = new[]
                            {
                                new RouteRegistration
                                {
                                    ReceiverIdentity = ReceiverIdentities.CreateForActor().Identity,
                                    MessageContracts = EnumerableExtensions.Produce(Randomizer.UInt16(2, 5),
                                                                                    () => new MessageContract
                                                                                          {
                                                                                              Identity = Guid.NewGuid().ToByteArray(),
                                                                                              Version = Randomizer.UInt16()
                                                                                          })
                                                                           .ToArray()
                                },
                                new RouteRegistration
                                {
                                    ReceiverIdentity = ReceiverIdentities.CreateForMessageHub().Identity,
                                }
                            }
               };
    }
}