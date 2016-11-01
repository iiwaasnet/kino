using System;
using System.Collections.Generic;


namespace Autofac.kino
{
    public class ApplicationConfiguration
    {
        public string RouterUri { get; set; }

        public string ScaleOutAddressUri { get; set; }

        public IEnumerable<RendezvousNode> RendezvousServers { get; set; }

        public TimeSpan HeartBeatSilenceBeforeRendezvousFailover { get; set; }

        public bool RunAsStandalone { get; set; }

        public bool DeferPeerConnection { get; set; }
    }
}