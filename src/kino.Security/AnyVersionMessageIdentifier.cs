using kino.Core;
using kino.Core.Framework;

namespace kino.Security
{
    public class AnyVersionMessageIdentifier : Identifier
    {
        private readonly int hashCode;

        public AnyVersionMessageIdentifier(MessageIdentifier messageIdentifier)
            : this(messageIdentifier.Identity)
        {
        }

        public AnyVersionMessageIdentifier(byte[] identity)
        {
            Identity = identity;
            hashCode = CalculateHashCode();
        }

        public override bool Equals(IIdentifier other)
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

        private bool StructuralCompare(IIdentifier other)
            => Unsafe.ArraysEqual(Identity, other.Identity);

        private int CalculateHashCode()
            => Identity.ComputeHash();

        public override int GetHashCode()
            => hashCode;

        public override string ToString()
            => $"{nameof(Identity)}[{Identity?.GetAnyString()}]";
    }
}