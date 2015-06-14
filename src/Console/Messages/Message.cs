namespace Console.Messages
{
    public class Message : IMessage
    {
        private static readonly IMessageSerializer messageSerializer;

        static Message()
        {
            messageSerializer = new MessageSerializer();
        }

        protected Message()
        {
        }

        public Message(Body body)
        {
            Body = body;
        }

        protected byte[] Serialize(object payload)
        {
            return messageSerializer.Serialize(payload);
        }

        protected static T Deserialize<T>(byte[] content)
        {
            return messageSerializer.Deserialize<T>(content);
        }

        public Body Body { get; protected set; }
    }
}