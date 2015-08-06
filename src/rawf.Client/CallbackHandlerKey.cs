using System;
using rawf.Framework;

namespace rawf.Client
{
    public class CallbackHandlerKey : IEquatable<CallbackHandlerKey>
    {
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
            return Equals((CallbackHandlerKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Version?.Length ?? 0);
                hashCode = (hashCode * 397) ^ (Correlation?.Length ?? 0);
                hashCode = (hashCode * 397) ^ (Identity?.Length ?? 0);
                return hashCode;
            }
        }

        public bool Equals(CallbackHandlerKey other)
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

        private bool StructuralCompare(CallbackHandlerKey other)
            => Unsafe.Equals(Correlation, other.Correlation)
               && Unsafe.Equals(Identity, other.Identity)
               && Unsafe.Equals(Version, other.Version);

        public byte[] Version { get; set; }
        public byte[] Identity { get; set; }
        public byte[] Correlation { get; set; }
    }
}