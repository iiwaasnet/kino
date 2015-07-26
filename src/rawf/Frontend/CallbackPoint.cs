namespace rawf.Frontend
{
    public class CallbackPoint : ICallbackPoint
    {
        public CallbackPoint(byte[] messageIdentity)
        {
            MessageIdentity = messageIdentity;
        }

        public byte[] MessageIdentity { get; }
    }
}