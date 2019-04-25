using System;
using Confluent.Kafka;

namespace kino.Connectivity.Kafka
{
    public class KafkaListenerConfiguration
    {
        public string BootstrapServers { get; set; }

        public AutoOffsetReset AutoOffsetReset { get; set; }

        public bool? SocketKeepAliveEnabled { get; set; }

        public TimeSpan? SessionTimeout { get; set; }

        public TimeSpan? MaxPollInterval { get; set; }
    }
}