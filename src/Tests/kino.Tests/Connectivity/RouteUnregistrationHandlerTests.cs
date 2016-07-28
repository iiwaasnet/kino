using System;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class RouteUnregistrationHandlerTests
    {
        private Mock<ILogger> logger;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<IClusterMembership> clusterMembership;
        private IExternalRoutingTable externalRoutingTable;
        private string securityDomain;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            clusterMembership = new Mock<IClusterMembership>();
            externalRoutingTable = new ExternalRoutingTable(logger.Object);
            securityDomain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.SecurityDomainIsAllowed(It.IsAny<string>())).Returns(true);
            securityProvider.Setup(m => m.GetAllowedSecurityDomains()).Returns(new[] {securityDomain});
            securityProvider.Setup(m => m.GetSecurityDomain(It.IsAny<byte[]>())).Returns(securityDomain);
        }

        [Test]
        public void DisconnectNotCalled_IfConnectWasNotCalledBefore()
        {
            var config = new RouterConfiguration {DeferPeerConnection = true};
            var registrationHandler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
                                                                                  clusterMembership.Object,
                                                                                  config,
                                                                                  securityProvider.Object,
                                                                                  logger.Object);
            var socket = new Mock<ISocket>();
            var peerUri = "tcp://127.0.0.1:80";
            var peerSocketIdentity = Guid.NewGuid().ToByteArray();
            var messageIdentifier = Guid.NewGuid().ToByteArray();
            var messageVersion = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = peerUri,
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentifier,
                                                                        Version = messageVersion
                                                                    }
                                                                },
                                             SocketIdentity = peerSocketIdentity
                                         },
                                         securityDomain);

            registrationHandler.Handle(message, socket.Object);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never());
            var identifier = new MessageIdentifier(messageVersion, messageIdentifier, IdentityExtensions.Empty);
            Assert.AreEqual(peerUri, externalRoutingTable.FindRoute(identifier, null).Node.Uri.ToSocketAddress());

            var unregistrationHandler = new NodeUnregistrationHandler(externalRoutingTable,
                                                                      clusterMembership.Object,
                                                                      securityProvider.Object);

            message = Message.Create(new UnregisterNodeMessage {Uri = peerUri, SocketIdentity = peerSocketIdentity}, securityDomain);
            unregistrationHandler.Handle(message, socket.Object);

            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Never());
        }

        [Test]
        public void DisconnectCalled_IfConnectWasCalledBefore()
        {
            var config = new RouterConfiguration {DeferPeerConnection = false};
            var registrationHandler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
                                                                                  clusterMembership.Object,
                                                                                  config,
                                                                                  securityProvider.Object,
                                                                                  logger.Object);
            var socket = new Mock<ISocket>();
            var peerUri = "tcp://127.0.0.1:80";
            var peerSocketIdentity = Guid.NewGuid().ToByteArray();
            var messageIdentifier = Guid.NewGuid().ToByteArray();
            var messageVersion = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = peerUri,
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentifier,
                                                                        Version = messageVersion
                                                                    }
                                                                },
                                             SocketIdentity = peerSocketIdentity
                                         },
                                         securityDomain);

            registrationHandler.Handle(message, socket.Object);

            var unregistrationHandler = new NodeUnregistrationHandler(externalRoutingTable,
                                                                      clusterMembership.Object,
                                                                      securityProvider.Object);

            message = Message.Create(new UnregisterNodeMessage {Uri = peerUri, SocketIdentity = peerSocketIdentity}, securityDomain);
            unregistrationHandler.Handle(message, socket.Object);

            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Once());
        }
    }
}