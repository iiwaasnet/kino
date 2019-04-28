using kino.Core;
using kino.Messaging.Kafka.Messages;

namespace kino.Routing.Kafka
{
    public class ExternalKafkaRouteRegistration
    {
        public MessageRoute Route { get; set; }

        public Node Peer { get; set; }

        public Health Health { get; set; }
    }
}