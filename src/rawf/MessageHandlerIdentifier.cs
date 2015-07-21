using System;
using rawf.Framework;

namespace rawf
{
    //TODO: Probably better to duplicate functionality for every derived class
    public class MessageHandlerIdentifier : IEquatable<MessageHandlerIdentifier>
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

        public override string ToString()
        {
            return string.Format($"Identity[{Identity?.GetString()}]-Version[{Version?.GetString()}]");
        }

        public byte[] Version { get; }
        public byte[] Identity { get; }
    }
}