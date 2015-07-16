namespace rawf.Messaging
{
    public class Payload : IPayload
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

        virtual public T Deserialize<T>(byte[] content)
        {
            return messageSerializer.Deserialize<T>(content);
        }

        virtual public byte[] Serialize()
        {
            return messageSerializer.Serialize(this);
        }
    }
}