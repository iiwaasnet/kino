using Console.Messages;

namespace Console
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