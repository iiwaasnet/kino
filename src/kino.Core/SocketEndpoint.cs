using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class SocketEndpoint : IEquatable<SocketEndpoint>
    {
        private readonly int hashCode;

        public SocketEndpoint(string uri)
            : this(uri.ParseAddress(), ReceiverIdentifier.CreateIdentity())
        {
        }

        public SocketEndpoint(string uri, byte[] identity)
            : this(uri.ParseAddress(), identity)
        {
        }

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
            if (obj.GetType() != GetType())
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
            => Unsafe.ArraysEqual(Identity, other.Identity)
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