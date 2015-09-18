using System;
using kino.Framework;

namespace kino.Connectivity
{
    public class MessageIdentifier : IEquatable<MessageIdentifier>
    {
        private readonly int hashCode;

        public MessageIdentifier(byte[] version, byte[] identity)
        {
            Version = version;
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
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((MessageIdentifier) obj);
        }

        private int CalculateHashCode()
        {
            unchecked
            {
                var hashCode = Version.Length;
                hashCode = (hashCode * 397) ^ Identity.Length;
                return hashCode;
            }
        }

        public override int GetHashCode()
            => hashCode;

        public bool Equals(MessageIdentifier other)
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

        private bool StructuralCompare(MessageIdentifier other)
            => Unsafe.Equals(Identity, other.Identity)
               && Unsafe.Equals(Version, other.Version);

        public override string ToString()
            => string.Format($"Identity[{Identity?.GetString()}]-Version[{Version?.GetString()}]");

        public byte[] Version { get; }
        public byte[] Identity { get; }
    }
}