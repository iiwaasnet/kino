using System;

namespace rawf.Connectivity
{
    public class AdsServerConfiguration
    {
        public Uri ClusterConfigurationEndpoint { get; set; }
        public Uri HeartBeatEndpoint { get; set; }
    }
}