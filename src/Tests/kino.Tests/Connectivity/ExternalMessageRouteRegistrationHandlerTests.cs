using System;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ExternalMessageRouteRegistrationHandlerTests
    {
        private Mock<ILogger> logger;
        private Mock<ISecurityProvider> securityProvider;
        private string domain;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            domain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
        }

        [Test]
        public void IfPeerConnectionIsDeferred_NoConnectionMadeToRemotePeer()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var clusterMembership = new Mock<IClusterMembership>();
            var config = new RouterConfiguration {DeferPeerConnection = true};
            var handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
                                                                      clusterMembership.Object,
                                                                      config,
                                                                      securityProvider.Object,
                                                                      logger.Object);
            var socket = new Mock<ISocket>();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = Guid.NewGuid().ToByteArray(),
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         });

            handler.Handle(message, socket.Object);

            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        }

        [Test]
        public void IfPeerConnectionIsNotDeferred_ConnectionMadeToRemotePeer()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var clusterMembership = new Mock<IClusterMembership>();
            var config = new RouterConfiguration {DeferPeerConnection = false};
            var handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
                                                                      clusterMembership.Object,
                                                                      config,
                                                                      securityProvider.Object,
                                                                      logger.Object);
            var socket = new Mock<ISocket>();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = Guid.NewGuid().ToByteArray(),
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);

            handler.Handle(message, socket.Object);

            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Once);
        }

        [Test]
        public void IfPeerConnectionIsNotDeferredButPeerIsAlreadyConnected_NoConnectionMadeToRemotePeer()
        {
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(), It.IsAny<SocketIdentifier>(), It.IsAny<Uri>()))
                                .Returns(new PeerConnection {Connected = true});
            var clusterMembership = new Mock<IClusterMembership>();
            var config = new RouterConfiguration {DeferPeerConnection = false};
            var handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable.Object,
                                                                      clusterMembership.Object,
                                                                      config,
                                                                      securityProvider.Object,
                                                                      logger.Object);
            var socket = new Mock<ISocket>();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = Guid.NewGuid().ToByteArray(),
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         });

            handler.Handle(message, socket.Object);

            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        }
    }
}