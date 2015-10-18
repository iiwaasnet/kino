using System.Collections.Generic;

namespace kino.Connectivity
{
    public class RendezvousClusterConfiguration
    {
        public IEnumerable<RendezvousEndpoint> Cluster { get; set; }
    }
}