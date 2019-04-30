using kino.Cluster.Kafka;
using kino.Core;

namespace kino.Routing.Kafka
{
    public class KafkaPeerConnection
    {
        public KafkaNode Node { get; set; }

        public bool Connected { get; set; }

        public Health Health { get; set; }
    }
}