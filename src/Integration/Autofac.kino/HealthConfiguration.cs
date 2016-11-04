using System;

namespace Autofac.kino
{
    public class HealthConfiguration
    {
        public string HeartBeatUri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }

        public string IntercomEndpoint { get; set; }

        public int MissingHeartBeatsBeforeDeletion { get; set; }
    }
}