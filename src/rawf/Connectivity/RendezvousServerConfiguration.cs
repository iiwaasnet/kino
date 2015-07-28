using System;

namespace rawf.Connectivity
{
    public class RendezvousServerConfiguration
    {
        public Uri ClusterConfigurationEndpoint { get; set; }
        public Uri HeartBeatEndpoint { get; set; }
    }
}