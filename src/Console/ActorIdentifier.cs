namespace Console
{
    internal class ActorIdentifier : MessageHandlerIdentifier
    {
        public ActorIdentifier(byte[] version, byte[] messageIdentity)
            : base(version, messageIdentity)
        {
        }
    }
}