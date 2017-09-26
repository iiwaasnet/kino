using System;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using Moq;
using Xunit;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class UnreachableNodeUnregistrationHandlerTests
    {
        private readonly UnreachableNodeUnregistrationHandler handler;
        private readonly Mock<IClusterHealthMonitor> clusterHealthMonitor;
        private readonly Mock<IExternalRoutingTable> externalRoutingTable;
        private readonly Mock<ISocket> backendSocket;

        public UnreachableNodeUnregistrationHandlerTests()
        {
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            backendSocket = new Mock<ISocket>();
            handler = new UnreachableNodeUnregistrationHandler(clusterHealthMonitor.Object,
                                                               externalRoutingTable.Object);
        }

        [Theory(DisplayName = "ScaleOutBackend socket disconnects peer only if PeerConnectionAction is Disconnect." +
                              "ClusterHealthMonitor deletes peer only if PeerConnectionAction is not KeepConnection.")]
        [InlineData(PeerConnectionAction.KeepConnection)]
        [InlineData(PeerConnectionAction.Disconnect)]
        [InlineData(PeerConnectionAction.NotFound)]
        public void PeerIsDisconnected_OnlyIfPeerConnectionActionIsDisconnect(PeerConnectionAction peerConnectionAction)
        {
            var receiverNodeIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new UnregisterUnreachableNodeMessage {ReceiverNodeIdentity = receiverNodeIdentifier.Identity};
            var message = Message.Create(payload);
            var peerRemoveResult = new PeerRemoveResult
                                   {
                                       ConnectionAction = peerConnectionAction,
                                       Uri = new Uri("tcp://127.0.0.1:9009")
                                   };
            externalRoutingTable.Setup(m => m.RemoveNodeRoute(receiverNodeIdentifier)).Returns(peerRemoveResult);
            //
            handler.Handle(message, backendSocket.Object);
            //
            externalRoutingTable.Verify(m => m.RemoveNodeRoute(receiverNodeIdentifier), Times.Once);
            backendSocket.Verify(m => m.Disconnect(peerRemoveResult.Uri), Times.Exactly(peerConnectionAction == PeerConnectionAction.Disconnect ? 1 : 0));
            clusterHealthMonitor.Verify(m => m.DeletePeer(receiverNodeIdentifier), Times.Exactly(peerConnectionAction != PeerConnectionAction.KeepConnection ? 1 : 0));
        }
    }
}