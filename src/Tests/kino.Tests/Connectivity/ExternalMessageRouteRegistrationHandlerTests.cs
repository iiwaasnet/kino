using System;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ExternalMessageRouteRegistrationHandlerTests
    {
        [Test]
        public void IfPeerConnectionIsDeferred_NoConnectionMadeToRemotePeer()
        {
            var logger = new Mock<ILogger>().Object;
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var clusterMembership = new Mock<IClusterMembership>();
            var config = new RouterConfiguration {DeferPeerConnection = true};
            var handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable, clusterMembership.Object, config, logger);
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
            var logger = new Mock<ILogger>().Object;
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var clusterMembership = new Mock<IClusterMembership>();
            var config = new RouterConfiguration {DeferPeerConnection = false};
            var handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable, clusterMembership.Object, config, logger);
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

            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Once);
        }


        [Test]
        public void IfPeerConnectionIsNotDeferredButPeerIsAlreadyConnected_NoConnectionMadeToRemotePeer()
        {
            var logger = new Mock<ILogger>().Object;
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(), It.IsAny<SocketIdentifier>(), It.IsAny<Uri>()))
                                .Returns(new PeerConnection {Connected = true});
            var clusterMembership = new Mock<IClusterMembership>();
            var config = new RouterConfiguration {DeferPeerConnection = false};
            var handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable.Object, clusterMembership.Object, config, logger);
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