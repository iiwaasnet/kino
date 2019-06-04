using kino.Cluster;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class NodeUnregistrationHandler : IKafkaServiceMessageHandler
    {
        private readonly IClusterHealthMonitor clusterHealthMonitor;
        private readonly IKafkaExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public NodeUnregistrationHandler(IClusterHealthMonitor clusterHealthMonitor,
                                         IKafkaExternalRoutingTable externalRoutingTable,
                                         ISecurityProvider securityProvider)
        {
            this.clusterHealthMonitor = clusterHealthMonitor;
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public void Handle(IMessage message, ISender scaleOutBackend)
        {
            if (securityProvider.DomainIsAllowed(message.Domain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                var payload = message.GetPayload<UnregisterNodeMessage>();

                var nodeIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
                var peerRemoveResult = externalRoutingTable.RemoveNodeRoute(nodeIdentifier);
                if (peerRemoveResult.ConnectionAction == BrokerConnectionAction.Disconnect)
                {
                    scaleOutBackend.Disconnect(peerRemoveResult.AppCluster.BrokerName);
                }
                if (peerRemoveResult.ConnectionAction != BrokerConnectionAction.KeepConnection)
                {
                    clusterHealthMonitor.DeletePeer(nodeIdentifier);
                }
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.UnregisterNode;
    }
}