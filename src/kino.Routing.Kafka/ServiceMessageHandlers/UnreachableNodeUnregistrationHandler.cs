using kino.Cluster;
using kino.Cluster.Kafka;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class UnreachableNodeUnregistrationHandler : IKafkaServiceMessageHandler
    {
        private readonly IKafkaClusterHealthMonitor clusterHealthMonitor;
        private readonly IKafkaExternalRoutingTable externalRoutingTable;

        public UnreachableNodeUnregistrationHandler(IKafkaClusterHealthMonitor clusterHealthMonitor,
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
            if (peerRemoveResult.ConnectionAction == BrokerConnectionAction.Disconnect)
            {
                scaleOutBackend.Disconnect(peerRemoveResult.AppCluster.BrokerName);
            }
            if (peerRemoveResult.ConnectionAction != BrokerConnectionAction.KeepConnection)
            {
                clusterHealthMonitor.DisconnectFromBroker(peerRemoveResult.AppCluster.BrokerName);
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.UnregisterUnreachableNode;
    }
}