using System.Collections.Generic;

namespace kino.Cluster.Configuration
{
    public class RendezvousClusterConfiguration
    {
        public IEnumerable<RendezvousEndpoint> Cluster { get; set; }
    }
}