using System;
using kino.Core.Framework;

namespace kino.Security
{
    public class EquatableIdentity : IEquatable<EquatableIdentity>
    {
        private readonly int hashCode;

        public EquatableIdentity(byte[] identity)
        {
            Identity = identity;
            hashCode = CalculateHashCode();
        }

        private int CalculateHashCode()
            => Identity.ComputeHash();

        public override int GetHashCode()
            => hashCode;

        public bool Equals(EquatableIdentity other)
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
            return Equals((EquatableIdentity) obj);
        }

        private bool StructuralCompare(EquatableIdentity other)
            => Unsafe.ArraysEqual(Identity, other.Identity);

        public static bool operator ==(EquatableIdentity left, EquatableIdentity right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(EquatableIdentity left, EquatableIdentity right)
            => !(left == right);

        public override string ToString()
            => $"{nameof(Identity)}[{Identity?.GetAnyString()}]";

        public byte[] Identity { get; }
    }
}