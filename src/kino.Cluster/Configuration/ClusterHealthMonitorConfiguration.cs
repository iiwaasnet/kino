using System;

namespace kino.Cluster.Configuration
{
    public class ClusterHealthMonitorConfiguration
    {
        public Uri IntercomEndpoint { get; set; }

        public int MissingHeartBeatsBeforeDeletion { get; set; }

        public TimeSpan StalePeersCheckInterval { get; set; }

        public TimeSpan PeerIsStaleAfter { get; set; }
    }
}