using System;

namespace kino.Connectivity
{
    public class ClusterMembershipConfiguration
    {        
        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
        public bool RunAsStandalone { get; set; }
    }
}