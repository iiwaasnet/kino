using System;
using kino.Core.Framework;

namespace kino.Core
{
    public class MessageIdentifier : Identifier
    {
        private int? hashCode;

        public MessageIdentifier(byte[] identity, ushort version, byte[] partition)
        {
            Version = version;
            Identity = identity;
            Partition = partition ?? IdentityExtensions.Empty;
        }

        public MessageIdentifier(IIdentifier messageIdentifier)
            : this(messageIdentifier.Identity, messageIdentifier.Version, messageIdentifier.Partition)
        {
        }

        public static MessageIdentifier Create<T>(byte[] partition)
            where T : IIdentifier, new()
        {
            var message = new T();
            return new MessageIdentifier(message.Identity, message.Version, partition);
        }

        public static MessageIdentifier Create<T>()
            where T : IIdentifier, new()
        {
            var message = new T();
            return new MessageIdentifier(message.Identity, message.Version, message.Partition);
        }

        public static MessageIdentifier Create(Type messageType, byte[] partition)
        {
            var message = (IIdentifier) Activator.CreateInstance(messageType);
            return new MessageIdentifier(message.Identity, message.Version, partition);
        }

        public static MessageIdentifier Create(Type messageType)
            => Create(messageType, IdentityExtensions.Empty);

        private int CalculateHashCode()
        {
            unchecked
            {
                var hashCode = Identity.ComputeHash();
                hashCode = (hashCode * 397) ^ Version;
                hashCode = (hashCode * 397) ^ Partition.ComputeHash();
                return hashCode;
            }
        }

        public override int GetHashCode()
            => (hashCode ?? (hashCode = CalculateHashCode())).Value;

        public override bool Equals(IIdentifier other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return GetHashCode() == other.GetHashCode() && StructuralCompare(other);
        }

        private bool StructuralCompare(IIdentifier other)
            => Unsafe.ArraysEqual(Identity, other.Identity)
            && Version == other.Version
            && Unsafe.ArraysEqual(Partition, other.Partition);
    }
}