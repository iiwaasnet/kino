using System;

namespace kino.Core.Connectivity
{
    public class ClusterMembershipConfiguration
    {        
        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
        public bool RunAsStandalone { get; set; }
    }
}