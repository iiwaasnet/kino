using kino.Connectivity.Kafka;

namespace kino.Cluster.Kafka
{
    public class KafkaRendezvousConfiguration : KafkaListenerConfiguration
    {
        public string Topic { get; set; }
    }
}