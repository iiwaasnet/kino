using System;
using Framework;

namespace Console
{
    internal class MessageIdentifier : IEquatable<MessageIdentifier>
    {
        public MessageIdentifier(byte[] version, byte[] messageId)
        {
            Version = version;
            MessageId = messageId;
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
            return obj.GetType() == GetType()
                   && StructuralCompare((MessageIdentifier) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Version != null ? Version.GetHashCode() : 0)*397) ^
                       (MessageId != null ? MessageId.GetHashCode() : 0);
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
            return Unsafe.Equals(MessageId, other.MessageId) && Unsafe.Equals(Version, other.Version);
        }


        internal byte[] Version { get; }
        internal byte[] MessageId { get; }
    }
}