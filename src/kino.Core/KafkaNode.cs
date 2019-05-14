using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class KafkaNode : IEquatable<KafkaNode>, IDestination
    {
        private readonly int hashCode;

        public KafkaNode(string brokerName,
                         string topic,
                         string queue,
                         byte[] identity)
        {
            BrokerName = brokerName;
            Identity = identity;
            Topic = topic;
            Queue = queue;

            hashCode = CalculateHashCode();
        }

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
            => Unsafe.ArraysEqual(Identity, other.Identity)
               && BrokerName == other.BrokerName;

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return (BrokerName.GetHashCode() * 397) ^ Identity.ComputeHash();
            }
        }

        public override string ToString()
            => $"{BrokerName}:{Identity.GetAnyString()}";

        public string BrokerName { get; }

        public byte[] Identity { get; }

        public string Topic { get; }

        public string Queue { get; }
    }
}