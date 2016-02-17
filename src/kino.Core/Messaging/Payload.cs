using kino.Core.Framework;

namespace kino.Core.Messaging
{
    public abstract class Payload : IPayload
    {
        private static readonly IMessageSerializer DefaultSerializer = new ProtobufMessageSerializer();
        private readonly IMessageSerializer messageSerializer;

        protected Payload(IMessageSerializer messageSerializer)
        {
            this.messageSerializer = messageSerializer;
        }

        protected Payload()
        {
            messageSerializer = DefaultSerializer;
        }


        protected static byte[] BuildFullIdentity(string identity)
            => (Message.KinoMessageNamespace + "." + identity).GetBytes();

        public virtual T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public virtual byte[] Serialize()
            => messageSerializer.Serialize(this);

        public abstract byte[] Version { get; }

        public abstract byte[] Identity { get; }
    }
}