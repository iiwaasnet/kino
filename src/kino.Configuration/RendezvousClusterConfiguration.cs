using System.Collections.Generic;

namespace kino.Configuration
{
    public class RendezvousClusterConfiguration
    {
        public IEnumerable<RendezvousEndpoint> Cluster { get; set; }
    }
}