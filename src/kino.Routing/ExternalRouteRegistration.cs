using kino.Cluster;
using kino.Core;

namespace kino.Routing
{
    public class ExternalRouteRegistration
    {
        public MessageRoute Route { get; set; }

        public Node Peer { get; set; }

        public Health Health { get; set; }
    }
}