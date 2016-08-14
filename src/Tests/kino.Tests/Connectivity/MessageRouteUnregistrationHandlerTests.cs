using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
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
    public class MessageRouteUnregistrationHandlerTests
    {
        private MessageRouteUnregistrationHandler handler;
        private Mock<ILogger> logger;
        private Mock<IExternalRoutingTable> externalRoutingTable;
        private string domain;
        private Mock<ISecurityProvider> securityProvider;
        private byte[] messageIdentity;
        private Mock<ISocket> socket;

        [SetUp]
        public void Setup()
        {
            socket = new Mock<ISocket>();
            logger = new Mock<ILogger>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(), It.IsAny<SocketIdentifier>(), It.IsAny<Uri>()))
                                .Returns(new PeerConnection {Connected = false});
            domain = Guid.NewGuid().ToString();
            messageIdentity = Guid.NewGuid().ToByteArray();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.GetDomain(messageIdentity)).Returns(domain);
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.DomainIsAllowed(It.Is<string>(d => d != domain))).Returns(false);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            handler = new MessageRouteUnregistrationHandler(externalRoutingTable.Object,
                                                            securityProvider.Object,
                                                            logger.Object);
        }

        [Test]
        public void IfUnregisterMessageRouteMessageComesFromNotAllowedDomain_RouteIsNotRemoved()
        {
            var domain = Guid.NewGuid().ToString();
            var message = Message.Create(new UnregisterMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            externalRoutingTable.Verify(m => m.RemoveMessageRoute(It.IsAny<IEnumerable<MessageIdentifier>>(),
                                                                  It.IsAny<SocketIdentifier>()),
                                        Times.Never);
            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Never);
        }

        [Test]
        public void IfDomainOfMessageToUnregisterIsNotAllowed_RouteIsNotRemoved()
        {
            var messageIdentity = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new UnregisterMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            externalRoutingTable.Verify(m => m.RemoveMessageRoute(It.Is<IEnumerable<MessageIdentifier>>(ids => !ids.Any()),
                                                                  It.IsAny<SocketIdentifier>()),
                                        Times.Once);
            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Never);
        }
    }
}