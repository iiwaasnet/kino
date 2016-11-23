namespace kino.Core
{
    public class SocketIdentifier : ReceiverIdentifier
    {
        private readonly int hashCode;

        public SocketIdentifier(byte[] identity)
            : base(identity)
        {
        }

        public static bool operator ==(SocketIdentifier left, SocketIdentifier right)
            => left != null && left.Equals(right);

        public static bool operator !=(SocketIdentifier left, SocketIdentifier right)
            => !(left == right);
    }
}