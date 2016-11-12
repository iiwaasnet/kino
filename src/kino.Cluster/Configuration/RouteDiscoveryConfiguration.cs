using System;

namespace kino.Cluster.Configuration
{
    public class RouteDiscoveryConfiguration
    {
        public TimeSpan SendingPeriod { get; set; }

        public int RequestsPerSend { get; set; }

        public int MaxRequestsQueueLength { get; set; }
    }
}