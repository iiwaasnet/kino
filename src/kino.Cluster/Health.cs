using System;

namespace kino.Cluster
{
    public class Health
    {
        public string Uri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}