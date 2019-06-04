using kino.Connectivity.Kafka;
using kino.Core;

namespace kino.Cluster.Kafka
{
    public class KafkaClusterHealthMonitor : IKafkaClusterHealthMonitor
    {
        private readonly IListener listener;

        public KafkaClusterHealthMonitor(IKafkaConnectionFactory connectionFactory)
            => listener = connectionFactory.CreateListener();

        public void StartPeerMonitoring(KafkaNode peer, Health health)
        {
            listener.Connect(peer.BrokerName);
            listener.Subscribe(peer.BrokerName, health.Topic);
        }

        public void ScheduleConnectivityCheck(ReceiverIdentifier nodeIdentifier)
        {
            throw new System.NotImplementedException();
        }

        public void AddPeer(KafkaNode peer, Health health)
            => StartPeerMonitoring(peer, health);

        public void DisconnectFromBroker(string brokerName)
            => listener.Disconnect(brokerName);

        public void Start()
        {
            throw new System.NotImplementedException();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }
    }
}