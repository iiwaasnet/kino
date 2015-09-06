using System;
using System.Collections.Generic;

namespace Client
{
    public class ApplicationConfiguration
    {
        public string RouterUri { get; set; }
        public string ScaleOutAddressUri { get; set; }
        public IEnumerable<RendezvousEndpoint> RendezvousServers { get; set; }
        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
        public TimeSpan PromiseExpirationEvaluationInterval { get; set; }
        public TimeSpan ExpectedPingInterval { get; set; }
    }
}