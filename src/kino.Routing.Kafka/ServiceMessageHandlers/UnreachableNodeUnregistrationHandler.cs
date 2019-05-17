using kino.Cluster;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class UnreachableNodeUnregistrationHandler : IKafkaServiceMessageHandler
    {
        private readonly IClusterHealthMonitor clusterHealthMonitor;
        private readonly IKafkaExternalRoutingTable externalRoutingTable;

        public UnreachableNodeUnregistrationHandler(IClusterHealthMonitor clusterHealthMonitor,
                                                    IKafkaExternalRoutingTable externalRoutingTable)
        {
            this.clusterHealthMonitor = clusterHealthMonitor;
            this.externalRoutingTable = externalRoutingTable;
        }

        public void Handle(IMessage message, ISender scaleOutBackend)
        {
            var payload = message.GetPayload<UnregisterUnreachableNodeMessage>();

            var nodeIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
            var peerRemoveResult = externalRoutingTable.RemoveNodeRoute(nodeIdentifier);
            if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
            {
                scaleOutBackend.Disconnect(peerRemoveResult.AppCluster.BrokerName);
            }
            if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
            {
                clusterHealthMonitor.DeletePeer(nodeIdentifier);
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.UnregisterUnreachableNode;
    }
}