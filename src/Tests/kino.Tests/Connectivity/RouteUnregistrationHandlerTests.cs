using System;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class RouteUnregistrationHandlerTests
    {
        [Test]
        public void TestDisconnectNotCalled_IfConnectWasNotCalledBefore()
        {
            var logger = new Mock<ILogger>().Object;
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var registrationHandler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable, logger);
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
                                         });

            registrationHandler.Handle(message, socket.Object);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never());
            Assert.AreEqual(peerUri, externalRoutingTable.FindRoute(new MessageIdentifier(messageVersion, messageIdentifier)).Node.Uri.ToSocketAddress());

            var unregistrationHandler = new RouteUnregistrationHandler(externalRoutingTable);

            message = Message.Create(new UnregisterNodeMessageRouteMessage {Uri = peerUri, SocketIdentity = peerSocketIdentity});
            unregistrationHandler.Handle(message, socket.Object);

            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Never());
        }

        [Test]
        public void TestDisconnectCalled_IfConnectWasCalledBefore()
        {
            var logger = new Mock<ILogger>().Object;
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var registrationHandler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable, logger);
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
                                         });

            registrationHandler.Handle(message, socket.Object);
            var peerConnection = externalRoutingTable.FindRoute(new MessageIdentifier(messageVersion, messageIdentifier));
            peerConnection.Connected = true;

            var unregistrationHandler = new RouteUnregistrationHandler(externalRoutingTable);

            message = Message.Create(new UnregisterNodeMessageRouteMessage {Uri = peerUri, SocketIdentity = peerSocketIdentity});
            unregistrationHandler.Handle(message, socket.Object);

            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Once());
        }
    }
}