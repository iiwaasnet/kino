using System;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class UnreachableNodeUnregistrationHandlerTests
    {
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IExternalRoutingTable> externalRoutingTable;
        private UnreachableNodeUnregistrationHandler handler;
        private Mock<ISocket> socket;

        [SetUp]
        public void Setup()
        {
            socket = new Mock<ISocket>();
            clusterMembership = new Mock<IClusterMembership>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            handler = new UnreachableNodeUnregistrationHandler(externalRoutingTable.Object, clusterMembership.Object);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void InregisterUnreachableNode_DeletesClusterMemberRemovesNodeFromExternalRoutingTableAndDisconnectsIt(bool disconnect)
        {
            var socketIdentity = Guid.NewGuid().ToByteArray();
            var uri = "tcp://localhost";
            externalRoutingTable.Setup(m => m.RemoveNodeRoute(new SocketIdentifier(socketIdentity)))
                                .Returns(disconnect
                                             ? PeerConnectionAction.Disconnect
                                             : PeerConnectionAction.None);
            var message = Message.Create(new UnregisterUnreachableNodeMessage
                                         {
                                             Uri = uri,
                                             SocketIdentity = socketIdentity
                                         },
                                         string.Empty);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.DeleteClusterMember(new SocketEndpoint(new Uri(uri), socketIdentity)), Times.Once);
            externalRoutingTable.Verify(m => m.RemoveNodeRoute(new SocketIdentifier(socketIdentity)), Times.Once);
            socket.Verify(m => m.Disconnect(new Uri(uri)), Times.Exactly(disconnect ? 1 : 0));
        }
    }
}