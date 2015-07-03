namespace rawf.Actors
{
    internal class ActorIdentifier : MessageHandlerIdentifier
    {
        public ActorIdentifier(byte[] version, byte[] messageIdentity)
            : base(version, messageIdentity)
        {
        }
    }
}