using System;

namespace kino.Configuration
{
    public class ClusterMembershipConfiguration
    {
        public TimeSpan HeartBeatSilenceBeforeRendezvousFailover { get; set; }

        public RouteDiscoveryConfiguration RouteDiscovery { get; set; }

        public bool RunAsStandalone { get; set; }
    }
}