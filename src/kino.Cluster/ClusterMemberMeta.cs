using System;

namespace kino.Cluster
{
    public class ClusterMemberMeta
    {
        public DateTime LastKnownHeartBeat { get; set; }

        public string HealthUri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}