using System;
using kino.Framework;

namespace kino.Messaging
{
    public class CorrelationId : IEquatable<CorrelationId>
    {
        public static readonly byte[] Infrastructural = {0, 0, 0};
        private readonly int hashCode;

        public CorrelationId(byte[] id)
        {
            Value = id;

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
            return Equals((CorrelationId) obj);
        }

        public override int GetHashCode()
            => hashCode;

        private int CalculateHashCode()
            => Value.ComputeHash();

        public bool Equals(CorrelationId other)
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

        private bool StructuralCompare(CorrelationId other)
            => Unsafe.Equals(Value, other.Value);

        public byte[] Value { get; }
    }
}