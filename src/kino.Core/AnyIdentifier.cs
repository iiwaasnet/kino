using kino.Core.Framework;

namespace kino.Core
{
    public class AnyIdentifier : Identifier
    {
        private readonly int hashCode;

        public AnyIdentifier()
        {
            Identity = new byte[0];
            Partition = new byte[0];

            hashCode = CalculateHashCode();
        }

        public override int GetHashCode()
            => hashCode;

        public override bool Equals(Identifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Unsafe.ArraysEqual(Identity, other.Identity);
        }

        private int CalculateHashCode()
            => Identity.ComputeHash();

        public override string ToString()
            => "[ANY MESSAGE]";
    }
}