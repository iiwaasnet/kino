namespace rawf.Actors
{
    public class ActorIdentifier : MessageHandlerIdentifier
    {
        public ActorIdentifier(byte[] version, byte[] messageIdentity)
            : base(version, messageIdentity)
        {
        }
    }
}