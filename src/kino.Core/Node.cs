using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class Node : IEquatable<Node>, IDestination
    {
        private readonly int hashCode;

        public Node(Uri uri, byte[] socketIdentity)
        {
            Uri = uri.ToSocketAddress();
            SocketIdentity = socketIdentity;

            hashCode = CalculateHashCode();
        }

        public Node(string uri, byte[] socketIdentity)
            : this(new Uri(uri), socketIdentity)
        {
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

            return Equals((Node) obj);
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

        public string Uri { get; }

        public byte[] SocketIdentity { get; }
    }
}