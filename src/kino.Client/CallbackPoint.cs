using kino.Messaging;

namespace kino.Client
{
    public class CallbackPoint : ICallbackPoint
    {
        public CallbackPoint(byte[] messageIdentity, byte[] messageVersion)
        {
            MessageIdentity = messageIdentity;
            MessageVersion = messageVersion;
        }

        public static ICallbackPoint Create<T>()
            where T : IMessageIdentifier, new()
        {
            var message = new T();
            return new CallbackPoint(message.Identity, message.Version);
        }

        public byte[] MessageIdentity { get; }
        public byte[] MessageVersion { get; }
    }
}