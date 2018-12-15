using System.Collections.Generic;
using kino.Cluster.Configuration;

namespace kino.Rendezvous.Configuration
{
    public class PartnerClusterConfiguration
    {
        public string NetworkId { get; set; }

        public IEnumerable<string> AllowedDomains { get; set; }

        public IEnumerable<RendezvousEndpoint> Cluster { get; set; }
    }
}