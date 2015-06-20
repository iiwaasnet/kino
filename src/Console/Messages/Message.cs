using System;

namespace Console.Messages
{
    public class Message : IMessage
    {
        protected const string MessagesVersion = "1.0";
        private static readonly IMessageSerializer messageSerializer = new MessageSerializer();
        private object payload;

        public Message(IPayload payload, string messageIdentity)
        {
            Body = Serialize(payload);
            Version = MessagesVersion;
            Identity = messageIdentity;
            Distribution = DistributionPattern.Unicast;
            TTL = TimeSpan.Zero;
        }

        internal Message(MultipartMessage multipartMessage)
        {
            Body = multipartMessage.GetMessageBody();
            Identity = multipartMessage.GetMessageIdentity().GetString();
            Version = multipartMessage.GetMessageVersion().GetString();
            TTL = multipartMessage.GetMessageTTL().GetTimeSpan();
            Distribution = multipartMessage.GetMessageDistributionPattern().GetEnum<DistributionPattern>();
        }

        public T GetPayload<T>()
            where T : IPayload
            => (T)(payload ?? (payload = Deserialize<T>(Body)));

        private static byte[] Serialize(object payload)
            => messageSerializer.Serialize(payload);

        private static T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public byte[] Body { get; private set; }
        public string Identity { get; private set; }
        public string Version { get; private set; }
        public TimeSpan TTL { get; set; }
        public DistributionPattern Distribution { get; set; }
    }
}