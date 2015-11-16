using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public class RendezvousClusterConfiguration
    {
        public IEnumerable<RendezvousEndpoint> Cluster { get; set; }
    }
}