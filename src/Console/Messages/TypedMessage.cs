namespace Console.Messages
{
    public class TypedMessage<T> : Message, ITypedMessage<T>
        where T : class
    {
        private T payload;
        private const string MessagesVersion = "1.0";

        protected TypedMessage(IMessage message)
            : base(message.Body, message.Identity)
        {
        }

        protected TypedMessage(T payload, string messageIdentity)
        {
            Body = Serialize(payload);
            Identity = messageIdentity;
            Distribution = DistributionPattern.Unicast;
            Version = MessagesVersion;
        }

        public T GetPayload()
            => payload ?? (payload = Deserialize<T>(Body));
    }
}