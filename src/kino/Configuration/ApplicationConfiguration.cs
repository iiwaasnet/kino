using System;
using System.Collections.Generic;

namespace kino.Configuration
{
    public class ApplicationConfiguration
    {
        public string ScaleOutAddressUri { get; set; }

        public int ScaleOutReceiveMessageQueueLength { get; set; }

        public IEnumerable<RendezvousNode> RendezvousServers { get; set; }

        public TimeSpan HeartBeatSilenceBeforeRendezvousFailover { get; set; }

        public bool RunAsStandalone { get; set; }

        public HealthConfiguration Health { get; set; }
    }
}