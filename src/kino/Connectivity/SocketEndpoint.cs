using System;
using kino.Framework;

namespace kino.Connectivity
{
    public class SocketEndpoint : IEquatable<SocketEndpoint>
    {
        private readonly int hashCode;

        public SocketEndpoint(Uri uri, byte[] identity)
        {
            Uri = uri;
            Identity = identity;

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
            return Equals((SocketEndpoint) obj);
        }

        public bool Equals(SocketEndpoint other)
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

        private bool StructuralCompare(SocketEndpoint other)
            => Unsafe.Equals(Identity, other.Identity)
               && Uri.AbsoluteUri == other.Uri.AbsoluteUri;

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return (Uri.GetHashCode() * 397) ^ Identity.ComputeHash();
            }
        }

        public Uri Uri { get; }
        public byte[] Identity { get; }
    }
}