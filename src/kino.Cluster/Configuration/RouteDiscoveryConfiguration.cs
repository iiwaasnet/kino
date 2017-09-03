using System;

namespace kino.Cluster.Configuration
{
    public class RouteDiscoveryConfiguration
    {
        public TimeSpan MissingRoutesDiscoverySendingPeriod { get; set; }

        public int MissingRoutesDiscoveryRequestsPerSend { get; set; }

        public int MaxMissingRouteDiscoveryRequestQueueLength { get; set; }

        public int MaxAutoDiscoverySenderQueueLength { get; set; }

        public TimeSpan UnregisterMessageSendTimeout { get; set; }

        public TimeSpan ClusterAutoDiscoveryPeriod { get; set; }

        public TimeSpan ClusterAutoDiscoveryStartDelay { get; set; }

        public int ClusterAutoDiscoveryStartDelayMaxMultiplier { get; set; }
    }
}