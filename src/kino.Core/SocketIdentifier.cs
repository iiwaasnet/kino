using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class SocketIdentifier : IEquatable<SocketIdentifier>
    {
        private readonly int hashCode;

        public SocketIdentifier(byte[] identity)
        {
            Identity = identity;

            hashCode = CalculateHashCode();
        }

        public static byte[] CreateIdentity()
            => Guid.NewGuid().ToString().GetBytes();

        public static SocketIdentifier Create()
            => new SocketIdentifier(CreateIdentity());

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
            return obj.GetType() == GetType()
                   && StructuralCompare((SocketIdentifier) obj);
        }

        public static bool operator ==(SocketIdentifier left, SocketIdentifier right)
            => left != null && left.Equals(right);

        public static bool operator !=(SocketIdentifier left, SocketIdentifier right)
            => !(left == right);

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
            => Identity.ComputeHash();

        public bool Equals(SocketIdentifier other)
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

        public override string ToString()
            => Identity.GetAnyString();

        private bool StructuralCompare(SocketIdentifier other)
            => Unsafe.ArraysEqual(Identity, other.Identity);

        public byte[] Identity { get; }
    }
}