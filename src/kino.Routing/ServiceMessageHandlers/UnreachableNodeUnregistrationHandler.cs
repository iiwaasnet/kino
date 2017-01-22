using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.ServiceMessageHandlers
{
    public class UnreachableNodeUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterHealthMonitor clusterHealthMonitor;
        private readonly IExternalRoutingTable externalRoutingTable;

        public UnreachableNodeUnregistrationHandler(IClusterHealthMonitor clusterHealthMonitor,
                                                    IExternalRoutingTable externalRoutingTable)
        {
            this.clusterHealthMonitor = clusterHealthMonitor;
            this.externalRoutingTable = externalRoutingTable;
        }

        public void Handle(IMessage message, ISocket scaleOutBackend)
        {
            var payload = message.GetPayload<UnregisterUnreachableNodeMessage>();

            var nodeIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
            var peerRemoveResult = externalRoutingTable.RemoveNodeRoute(nodeIdentifier);
            if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
            {
                scaleOutBackend.SafeDisconnect(peerRemoveResult.Uri);
            }
            if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
            {
                clusterHealthMonitor.DeletePeer(nodeIdentifier);
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.UnregisterUnreachableNode;
    }
}