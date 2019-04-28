using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class KafkaNode: IEquatable<KafkaNode>, IDestination
    {
        private readonly int hashCode;

        public KafkaNode(string brokerUri, 
                         string topic, 
                         string queue, 
                         byte[] nodeIdentity)
        {
            BrokerUri = brokerUri;
            NodeIdentity = nodeIdentity;
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

            return Equals((Node)obj);
        }

        public bool Equals(Node other)
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

            return StructuralCompare(other.As<Node>());
        }

        private bool StructuralCompare(Node other)
            => Unsafe.ArraysEqual(SocketIdentity, other.SocketIdentity)
            && Uri == other.Uri;

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return (Uri.GetHashCode() * 397) ^ SocketIdentity.ComputeHash();
            }
        }

        public override string ToString()
            => $"{SocketIdentity.GetAnyString()}@{Uri}";

        public string BrokerUri { get; }

        public byte[] NodeIdentity { get; }

        public string Topic { get; }

        public string Queue { get; }
    }
}