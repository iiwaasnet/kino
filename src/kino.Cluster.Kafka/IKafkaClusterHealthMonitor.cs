using kino.Core;

namespace kino.Cluster.Kafka
{
    public interface IKafkaClusterHealthMonitor
    {
        void StartPeerMonitoring(KafkaNode peer, Health health);

        void ScheduleConnectivityCheck(ReceiverIdentifier nodeIdentifier);

        void AddPeer(KafkaNode peer, Health health);

        void DisconnectFromBroker(string brokerName);

        void Start();

        void Stop();
    }
}