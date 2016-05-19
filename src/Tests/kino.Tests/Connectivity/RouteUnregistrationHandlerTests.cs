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
        private ILogger logger;
        private IClusterMembership clusterMembership;
        private IExternalRoutingTable externalRoutingTable;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>().Object;
            clusterMembership = new Mock<IClusterMembership>().Object;
            externalRoutingTable = new ExternalRoutingTable(logger);
        }

        [Test]
        public void DisconnectNotCalled_IfConnectWasNotCalledBefore()
        {            
            var registrationHandler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable,clusterMembership, logger);
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
            Assert.AreEqual(peerUri, externalRoutingTable.FindRoute(new MessageIdentifier(messageVersion, messageIdentifier, IdentityExtensions.Empty), null).Node.Uri.ToSocketAddress());

            var unregistrationHandler = new RouteUnregistrationHandler(externalRoutingTable, clusterMembership);

            message = Message.Create(new UnregisterNodeMessageRouteMessage {Uri = peerUri, SocketIdentity = peerSocketIdentity});
            unregistrationHandler.Handle(message, socket.Object);

            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Never());
        }

        [Test]
        public void DisconnectCalled_IfConnectWasCalledBefore()
        {
            var registrationHandler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable, clusterMembership,  logger);
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
            var peerConnection = externalRoutingTable.FindRoute(new MessageIdentifier(messageVersion, messageIdentifier, IdentityExtensions.Empty), null);
            peerConnection.Connected = true;

            var unregistrationHandler = new RouteUnregistrationHandler(externalRoutingTable, clusterMembership);

            message = Message.Create(new UnregisterNodeMessageRouteMessage {Uri = peerUri, SocketIdentity = peerSocketIdentity});
            unregistrationHandler.Handle(message, socket.Object);

            socket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Once());
        }
    }
}