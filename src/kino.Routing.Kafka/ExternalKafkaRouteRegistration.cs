using kino.Core;
using kino.Cluster.Kafka;

namespace kino.Routing.Kafka
{
    public class ExternalKafkaRouteRegistration
    {
        public MessageRoute Route { get; set; }

        public KafkaNode Node { get; set; }

        public Health Health { get; set; }
    }
}