using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class NodeUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterHealthMonitor clusterHealthMonitor;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public NodeUnregistrationHandler(IClusterHealthMonitor clusterHealthMonitor,
                                         IExternalRoutingTable externalRoutingTable,
                                         ISecurityProvider securityProvider)
        {
            this.clusterHealthMonitor = clusterHealthMonitor;
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public void Handle(IMessage message, ISocket scaleOutBackend)
        {
            if (securityProvider.DomainIsAllowed(message.Domain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                var payload = message.GetPayload<UnregisterNodeMessage>();

                var nodeIdentifer = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
                var peerRemoveResult = externalRoutingTable.RemoveNodeRoute(nodeIdentifer);
                if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
                {
                    scaleOutBackend.SafeDisconnect(peerRemoveResult.Uri);
                }
                if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
                {
                    clusterHealthMonitor.DeletePeer(nodeIdentifer);
                }
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.UnregisterNode;
    }
}