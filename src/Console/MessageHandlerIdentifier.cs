using System;
using Framework;

namespace Console
{
    internal abstract class MessageHandlerIdentifier : IEquatable<MessageHandlerIdentifier>
    {
        public MessageHandlerIdentifier(byte[] version, byte[] identity)
        {
            Version = version;
            Identity = identity;
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
            return Equals((MessageHandlerIdentifier) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Version?.Length ?? 0);
                hashCode = (hashCode*397) ^ (Identity?.Length ?? 0);
                return hashCode;
            }
        }


        public bool Equals(MessageHandlerIdentifier other)
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

        private bool StructuralCompare(MessageHandlerIdentifier other)
        {
            return Unsafe.Equals(Identity, other.Identity)
                   && Unsafe.Equals(Version, other.Version);
        }


        internal byte[] Version { get; }
        internal byte[] Identity { get; }
    }
}