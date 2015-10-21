using System;
using kino.Framework;
using kino.Messaging;

namespace kino.Connectivity
{
    public class MessageIdentifier : IEquatable<IMessageIdentifier>, IMessageIdentifier
    {
        private readonly int hashCode;

        public MessageIdentifier(byte[] version, byte[] identity)
        {
            Version = version;
            Identity = identity;

            hashCode = CalculateHashCode();
        }

        internal MessageIdentifier(byte[] identity)
            :this(IdentityExtensions.Empty, identity)
        {
        }

        public static IMessageIdentifier Create<T>()
            where T: IMessageIdentifier, new()
        {
            var message = new T();
            return new MessageIdentifier(message.Version, message.Identity);
        }

        public static IMessageIdentifier Create(Type messageType)
        {
            var message = (IMessageIdentifier)Activator.CreateInstance(messageType);
            return new MessageIdentifier(message.Version, message.Identity);
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
            return Equals((IMessageIdentifier) obj);
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

        public bool Equals(IMessageIdentifier other)
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

        private bool StructuralCompare(IMessageIdentifier other)
            => Unsafe.Equals(Identity, other.Identity)
               && Unsafe.Equals(Version, other.Version);

        public override string ToString()
            => string.Format($"Identity[{Identity?.GetString()}]-Version[{Version?.GetString()}]");

        public byte[] Version { get; }
        public byte[] Identity { get; }
    }
}