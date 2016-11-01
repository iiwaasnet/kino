using kino.Cluster;
using kino.Core;

namespace kino.Routing
{
    public class ExternalRouteDefinition
    {
        public Identifier Identifier { get; set; }

        public Node Peer { get; set; }

        public Health Health { get; set; }
    }
}