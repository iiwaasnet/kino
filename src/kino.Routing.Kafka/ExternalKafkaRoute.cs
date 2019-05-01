using System.Collections.Generic;
using kino.Core;

namespace kino.Routing.Kafka
{
    public class ExternalKafkaRoute
    {
        public KafkaNode Node { get; set; }

        public IEnumerable<MessageHubRoute> MessageHubs { get; set; }

        public IEnumerable<MessageActorRoute> MessageRoutes { get; set; }
    }
}