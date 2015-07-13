namespace rawf.Client
{
    public class CallbackIdentifier : MessageHandlerIdentifier
    {
        public CallbackIdentifier(byte[] version, byte[] receiverIdentity)
            : base(version, receiverIdentity)
        {
        }
    }
}