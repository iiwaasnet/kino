using System;

namespace kino.Cluster
{
    public class ClusterMemberMeta
    {
        public DateTime LastKnownHeartBeat { get; set; }

        public string HealthUri { get; set; }

        public string ScaleOutUri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }

        public bool ConnectionEstablished { get; set; }
    }
}