using System;
using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class MessageIdentifier : IEquatable<MessageIdentifier>, IMessageIdentifier
    {
        private readonly int hashCode;

        internal MessageIdentifier(byte[] version, byte[] identity, byte[] partition)
        {
            Version = version;
            Identity = identity;
            Partition = partition ?? IdentityExtensions.Empty;

            hashCode = CalculateHashCode();
        }

        public MessageIdentifier(IMessageIdentifier messageIdentifier)
            : this(messageIdentifier.Version, messageIdentifier.Identity, messageIdentifier.Partition)
        {
        }

        internal MessageIdentifier(byte[] identity)
            : this(IdentityExtensions.Empty, identity, IdentityExtensions.Empty)
        {
        }

        public static MessageIdentifier Create<T>(byte[] partition)
            where T : IMessageIdentifier, new()
        {
            var message = new T();
            return new MessageIdentifier(message.Version, message.Identity, partition);
        }

        public static MessageIdentifier Create<T>()
            where T : IMessageIdentifier, new()
        {
            var message = new T();
            return new MessageIdentifier(message.Version, message.Identity, message.Partition);
        }

        public static MessageIdentifier Create(Type messageType, byte[] partition)
        {
            var message = (IMessageIdentifier) Activator.CreateInstance(messageType);
            return new MessageIdentifier(message.Version, message.Identity, partition);
        }

        public static MessageIdentifier Create(Type messageType)
            => Create(messageType, IdentityExtensions.Empty);

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
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((MessageIdentifier) obj);
        }

        private int CalculateHashCode()
        {
            unchecked
            {
                var hashCode = Identity.ComputeHash();
                hashCode = (hashCode * 397) ^ Version.ComputeHash();
                hashCode = (hashCode * 397) ^ Partition.ComputeHash();
                return hashCode;
            }
        }

        public static bool operator ==(MessageIdentifier left, MessageIdentifier right)
            => left != null && left.Equals(right);

        public static bool operator !=(MessageIdentifier left, MessageIdentifier right)
            => !(left == right);

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
               && Unsafe.Equals(Version, other.Version)
               && Unsafe.Equals(Partition, other.Partition);

        public override string ToString()
            => $"{nameof(Identity)}[{Identity?.GetAnyString()}]-" +
               $"{nameof(Version)}[{Version?.GetAnyString()}]-" +
               $"{nameof(Partition)}[{Partition?.GetAnyString()}]";

        public byte[] Version { get; }

        public byte[] Identity { get; }

        public byte[] Partition { get; }
    }
}