using System;

namespace kino.Connectivity
{
    public class ClusterTimingConfiguration
    {
        public bool RunStandalone { get; set; }
        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
    }
}