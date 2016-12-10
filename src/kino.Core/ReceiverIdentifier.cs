using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class ReceiverIdentifier : IEquatable<ReceiverIdentifier>
    {
        private readonly int hashCode;

        public ReceiverIdentifier(byte[] identity)
        {
            Identity = identity;

            hashCode = CalculateHashCode();
        }

        public static byte[] CreateIdentity()
            => Guid.NewGuid().ToString().GetBytes();

        public static ReceiverIdentifier Create()
            => new ReceiverIdentifier(CreateIdentity());

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
                   && StructuralCompare((ReceiverIdentifier)obj);
        }

        //public static bool operator ==(ReceiverIdentifier left, ReceiverIdentifier right)
        //    => left != null && left.Equals(right);

        //public static bool operator !=(ReceiverIdentifier left, ReceiverIdentifier right)
        //    => !(left == right);

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
            => Identity.ComputeHash();

        public bool Equals(ReceiverIdentifier other)
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

        private bool StructuralCompare(ReceiverIdentifier other)
            => Unsafe.ArraysEqual(Identity, other.Identity);

        public byte[] Identity { get; }
    }
}