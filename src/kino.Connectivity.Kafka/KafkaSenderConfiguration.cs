using System;

namespace kino.Connectivity.Kafka
{
    public class KafkaSenderConfiguration
    {
        public string BootstrapServers { get; set; }

        public bool? SocketKeepAliveEnabled { get; set; }

        public TimeSpan? Linger { get; set; }
    }
}