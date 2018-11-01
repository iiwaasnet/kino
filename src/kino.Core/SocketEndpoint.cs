using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class SocketEndpoint : IEquatable<SocketEndpoint>
    {
        private int? hashCode;

        private SocketEndpoint(string uri, byte[] identity)
        {
            Uri = uri;
            Identity = identity;
        }

        public static SocketEndpoint FromTrustedSource(string uri, byte[] identity)
            => new SocketEndpoint(uri, identity);

        public static SocketEndpoint Parse(string uri, byte[] identity)
            => new SocketEndpoint(uri.ParseAddress().ToSocketAddress(), identity);

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
            => (hashCode ?? (hashCode = CalculateHashCode())).Value;

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