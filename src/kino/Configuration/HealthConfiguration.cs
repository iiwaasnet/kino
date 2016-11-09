using System;

namespace kino.Configuration
{
    public class HealthConfiguration
    {
        public string HeartBeatUri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }

        public string IntercomEndpoint { get; set; }

        public int MissingHeartBeatsBeforeDeletion { get; set; }

        public TimeSpan PeerIsStaleAfter { get; set; }

        public TimeSpan StalePeersCheckInterval { get; set; }
    }
}