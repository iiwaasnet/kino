using System;

namespace kino.Cluster.Configuration
{
    public class RouteDiscoveryConfiguration
    {
        public TimeSpan SendingPeriod { get; set; }

        public int RequestsPerSend { get; set; }

        public int MaxRouteDiscoveryRequestQueueLength { get; set; }

        public int MaxAutoDiscoverySenderQueueLength { get; set; }

        public TimeSpan ClusterAutoDiscoveryPeriod { get; set; }
    }
}