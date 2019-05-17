using System;

namespace kino.Connectivity.Kafka
{
    public class KafkaSenderConfiguration
    {
        public bool? SocketKeepAliveEnabled { get; set; }

        public TimeSpan? Linger { get; set; }
    }
}