namespace Console.Messages
{
    public class TypedMessage<T> : Message, ITypedMessage<T>
        where T : class
    {
        private T payload;

        protected TypedMessage(IMessage message)
            : base(message.Content, message.Type)
        {
        }

        protected TypedMessage(T payload, string messageType)
        {
            Type = messageType;
            Content = Serialize(payload);
        }

        public T GetPayload() 
            => payload ?? (payload = Deserialize<T>(Content));
    }
}