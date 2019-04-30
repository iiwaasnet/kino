﻿using System;

namespace kino.Routing.Kafka
{
    public class KafkaAppCluster : IEquatable<KafkaAppCluster>
    {
        public bool Equals(KafkaAppCluster other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // TODO: Optimize to compare only Topic and BrokerUri
            return Topic == other.Topic
                   && Queue == other.Queue
                   && BrokerUri == other.BrokerUri;
        }

        public override string ToString()
            => $"{Topic}-{Queue}@{BrokerUri}";

        public string BrokerUri { get; set; }

        public string Topic { get; set; }

        public string Queue { get; set; }
    }
}