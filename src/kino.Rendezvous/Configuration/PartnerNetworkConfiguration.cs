using System;
using System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public class PartnerNetworkConfiguration
    {
        public TimeSpan HeartBeatSilenceBeforeRendezvousFailover { get; set; }

        public string NetworkId { get; set; }

        public IEnumerable<string> AllowedDomains { get; set; }

        public IEnumerable<string> Cluster { get; set; }
    }
}