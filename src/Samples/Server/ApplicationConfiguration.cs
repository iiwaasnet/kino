﻿using System;
using System.Collections.Generic;
using kino.Actors;

namespace Server
{
    public class ApplicationConfiguration
    {
        public string RouterUri { get; set; }
        public string ScaleOutAddressUri { get; set; }
        public IEnumerable<RendezvousEndpoint> RendezvousServers { get; set; }
        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
        public bool RunAsStandalone { get; set; }
    }
}