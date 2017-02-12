using System;
using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ClusterMembershipConfiguration
    {
        public TimeSpan HeartBeatSilenceBeforeRendezvousFailover { get; set; }

        public RouteDiscoveryConfiguration RouteDiscovery { get; set; }

        public bool RunAsStandalone { get; set; }        
    }
}