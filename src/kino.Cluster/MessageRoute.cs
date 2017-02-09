using System;
using kino.Core;

namespace kino.Cluster
{
    public class MessageRoute : IEquatable<MessageRoute>
    {
        public bool Equals(MessageRoute other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Receiver.Equals(other.Receiver) && Message.Equals(other.Message);
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

            return Equals((MessageRoute) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Receiver != null ? Receiver.GetHashCode() : 0) * 397)
                       ^ (Message != null ? Message.GetHashCode() : 0);
            }
        }

        public static bool operator ==(MessageRoute left, MessageRoute right)
        {
            if (ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(MessageRoute left, MessageRoute right)
            => !(left == right);

        public ReceiverIdentifier Receiver { get; set; }

        public MessageIdentifier Message { get; set; }
    }
}