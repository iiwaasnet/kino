using System;

namespace kino.Configuration
{
    public class ClusterHealthMonitorConfiguration
    {
        public Uri IntercomEndpoint { get; set; }

        public int MissingHeartBeatsBeforeDeletion { get; set; }
    }
}