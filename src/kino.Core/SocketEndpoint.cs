using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class SocketEndpoint : IEquatable<SocketEndpoint>
    {
        private readonly int hashCode;

        private SocketEndpoint(string uri)
            : this(uri.ParseAddress(), ReceiverIdentifier.CreateIdentity())
        {
        }

        private SocketEndpoint(string uri, byte[] identity)
            : this(uri.ParseAddress(), identity)
        {
        }

        public SocketEndpoint(Uri uri, byte[] identity)
        {
            Uri = uri.ToSocketAddress();
            Identity = identity;

            hashCode = CalculateHashCode();
        }

        public static SocketEndpoint Resolve(string uri)
            => new SocketEndpoint(uri);

        public static SocketEndpoint Resolve(string uri, byte[] identity)
            => new SocketEndpoint(uri, identity);

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
            && Uri == other.Uri;

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
        {
            unchecked
            {
                return (Uri.GetHashCode() * 397) ^ Identity.ComputeHash();
            }
        }

        public string Uri { get; }

        public byte[] Identity { get; }
    }
}