using System;
using kino.Framework;

namespace kino.Connectivity
{
    //TODO: Probably better to duplicate functionality for every derived class
    public class MessageHandlerIdentifier : IEquatable<MessageHandlerIdentifier>
    {
        private readonly int hashCode;

        public MessageHandlerIdentifier(byte[] version, byte[] identity)
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
            return Equals((MessageHandlerIdentifier) obj);
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
            => Unsafe.Equals(Identity, other.Identity)
               && Unsafe.Equals(Version, other.Version);

        public override string ToString()
            => string.Format($"Identity[{Identity?.GetString()}]-Version[{Version?.GetString()}]");

        public byte[] Version { get; }
        public byte[] Identity { get; }
    }
}