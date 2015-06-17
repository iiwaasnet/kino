namespace Console.Messages
{
    public class Message : IMessage
    {
        private static readonly IMessageSerializer messageSerializer = new MessageSerializer();

        protected Message()
        {
        }

        protected Message(byte[] body, string msgIdentity)
        {
            Body = body;
            Identity = msgIdentity;
        }

        protected byte[] Serialize(object payload)
            => messageSerializer.Serialize(payload);

        protected static T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public byte[] Body { get; protected set; }
        public string Identity { get; protected set; }
        public string Version { get; protected set; }
        public long TTL { get; set; }
        public DistributionPattern Distribution { get; protected set; }
    }
}