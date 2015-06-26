using System;
using Framework;

namespace Console
{
    internal class MessageIdentifier : IEquatable<MessageIdentifier>
    {
        public MessageIdentifier(byte[] version, byte[] messageIdentity, byte[] receiverIdentity)
        {
            Version = version;
            MessageIdentity = messageIdentity;
            ReceiverIdentity = receiverIdentity;
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

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Version != null ? Version.Length : 0);
                hashCode = (hashCode*397) ^ (MessageIdentity != null ? MessageIdentity.Length : 0);
                hashCode = (hashCode*397) ^ (ReceiverIdentity != null ? ReceiverIdentity.Length : 0);
                return hashCode;
            }
        }


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
        {
            return Unsafe.Equals(MessageIdentity, other.MessageIdentity) 
                && Unsafe.Equals(Version, other.Version)
                && Unsafe.Equals(ReceiverIdentity, other.ReceiverIdentity);
        }


        internal byte[] Version { get; }
        internal byte[] MessageIdentity { get; }
        internal byte[] ReceiverIdentity { get; }
    }
}