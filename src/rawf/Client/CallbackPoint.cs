using rawf.Messaging;

namespace rawf.Client
{
    public class CallbackPoint : ICallbackPoint
    {
        public CallbackPoint(string messageIdentity)
        {
            MessageIdentity = messageIdentity.GetBytes();
        }

        public byte[] MessageIdentity { get; }
    }
}