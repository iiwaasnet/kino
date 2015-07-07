using rawf.Framework;
using rawf.Messaging;

namespace rawf.Client
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