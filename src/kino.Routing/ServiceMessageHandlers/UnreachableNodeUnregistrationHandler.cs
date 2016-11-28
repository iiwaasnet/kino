using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.ServiceMessageHandlers
{
    public class UnreachableNodeUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterConnectivity clusterConnectivity;
        private readonly IExternalRoutingTable externalRoutingTable;

        public UnreachableNodeUnregistrationHandler(IClusterConnectivity clusterConnectivity,
                                                    IExternalRoutingTable externalRoutingTable)
        {
            this.clusterConnectivity = clusterConnectivity;
            this.externalRoutingTable = externalRoutingTable;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterUnreachableNodeMessage>();

                var nodeIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
                var peerRemoveResult = externalRoutingTable.RemoveNodeRoute(nodeIdentifier);
                if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
                {
                    forwardingSocket.SafeDisconnect(peerRemoveResult.Uri);
                }
                if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
                {
                    clusterConnectivity.DeletePeer(nodeIdentifier);
                }
            }

            return shouldHandle;
        }

        private static bool IsUnregisterRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterUnreachableNode);
    }
}