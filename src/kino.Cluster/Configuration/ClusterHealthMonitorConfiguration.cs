using System;
using System.Diagnostics.CodeAnalysis;

namespace kino.Cluster.Configuration
{
    [ExcludeFromCodeCoverage]
    public class ClusterHealthMonitorConfiguration
    {
        public Uri IntercomEndpoint { get; set; }

        public int MissingHeartBeatsBeforeDeletion { get; set; }

        public TimeSpan StalePeersCheckInterval { get; set; }

        public TimeSpan PeerIsStaleAfter { get; set; }
    }
}