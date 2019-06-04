using System;

namespace kino.Cluster.Kafka
{
    public class Health
    {
        public string Topic { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}