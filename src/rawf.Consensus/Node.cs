using System;
using rawf.Framework;

namespace rawf.Consensus
{
    public class Node : INode, IEquatable<Node>
    {
        private readonly int hashCode;

        public Node(Uri uri, byte[] socketIdentity)
        {
            Uri = uri;
            SocketIdentity = socketIdentity;

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
            if (obj.GetType() != this.GetType())
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

        private bool StructuralCompare(INode other)
            => Unsafe.Equals(SocketIdentity, other.SocketIdentity)
               && Uri.AbsoluteUri == other.Uri.AbsoluteUri;

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return (Uri.GetHashCode() * 397) ^ SocketIdentity[0];
            }
        }

        public Uri Uri { get; }
        public byte[] SocketIdentity { get; }
    }
}