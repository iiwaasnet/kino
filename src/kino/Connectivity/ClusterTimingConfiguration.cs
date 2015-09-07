using System;

namespace kino.Connectivity
{
    public class ClusterTimingConfiguration
    {
        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
    }
}