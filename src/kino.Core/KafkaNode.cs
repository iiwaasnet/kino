using System;
using System.Linq;
using kino.Core.Framework;

namespace kino.Core
{
    public class KafkaNode : IEquatable<KafkaNode>, IDestination
    {
        private readonly int hashCode;

        public KafkaNode(string brokerUri,
                         string topic,
                         string queue,
                         byte[] nodeIdentity)
        {
            BrokerUri = Normalize(brokerUri);
            NodeIdentity = nodeIdentity;
            Topic = topic;
            Queue = queue;

            hashCode = CalculateHashCode();
        }

        private string Normalize(string brokerUri)
            => string.Join(",", brokerUri.Split(',').OrderBy(_ => _));

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((KafkaNode) obj);
        }

        public bool Equals(KafkaNode other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StructuralCompare(other);
        }

        public bool Equals(IDestination other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other.GetType() != GetType())
            {
                return false;
            }

            return StructuralCompare(other.As<KafkaNode>());
        }

        private bool StructuralCompare(KafkaNode other)
            => Unsafe.ArraysEqual(NodeIdentity, other.NodeIdentity)
               && Topic == other.Topic;

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return (Topic.GetHashCode() * 397) ^ NodeIdentity.ComputeHash();
            }
        }

        public override string ToString()
            => $"{Topic}:{NodeIdentity.GetAnyString()}";

        public string BrokerUri { get; }

        public byte[] NodeIdentity { get; }

        public string Topic { get; }

        public string Queue { get; }
    }
}