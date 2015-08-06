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

        public virtual T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public virtual byte[] Serialize()
            => messageSerializer.Serialize(this);
    }
}