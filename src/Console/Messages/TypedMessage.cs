namespace Console.Messages
{
    public class TypedMessage<T> : Message, ITypedMessage<T>
        where T : class
    {
        private T payload;

        protected TypedMessage(IMessage message)
            : base(message.Body)
        {
        }

        protected TypedMessage(T payload, string messageType)
        {
            
            Body = new Body
                   {
                       MessageType = messageType,
                       Content = Serialize(payload)
                   };
        }

        public T GetPayload()
        {
            return payload ?? (payload = Deserialize<T>(Body.Content));
        }
    }
}