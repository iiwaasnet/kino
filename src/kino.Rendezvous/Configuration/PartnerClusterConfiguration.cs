using System;
using System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public class PartnerClusterConfiguration
    {
        public string NetworkId { get; set; }

        public TimeSpan HeartBeatSilenceBeforeRendezvousFailover { get; set; }

        public IEnumerable<string> AllowedDomains { get; set; }

        public IEnumerable<PartnerRendezvousEndpoint> Cluster { get; set; }
    }
}