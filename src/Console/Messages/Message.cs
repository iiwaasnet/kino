using System;
using Framework;

namespace Console.Messages
{
    public class Message : IMessage
    {
        protected const string MessagesVersion = "1.0";
        private static readonly IMessageSerializer messageSerializer = new MessageSerializer();
        private object payload;

        private Message(IPayload payload, string messageIdentity)
        {
            Body = Serialize(payload);
            Version = MessagesVersion;
            Identity = messageIdentity;
            Distribution = DistributionPattern.Unicast;
            TTL = TimeSpan.Zero;
        }

        public static IMessage CreateFlowStartMessage(IPayload payload, string messageIdentity)
        {
            return new Message(payload, messageIdentity) {CorrelationId = GenerateCorrelationId() };
        }

        public static IMessage Create(IPayload payload, string messageIdentity)
        {
            return new Message(payload, messageIdentity);
        }

        private static byte[] GenerateCorrelationId()
        {
            //TODO: Better implementation
            return Guid.NewGuid().ToByteArray();
        }

        internal Message(MultipartMessage multipartMessage)
        {
            Body = multipartMessage.GetMessageBody();
            Identity = multipartMessage.GetMessageIdentity().GetString();
            Version = multipartMessage.GetMessageVersion().GetString();
            TTL = multipartMessage.GetMessageTTL().GetTimeSpan();
            Distribution = multipartMessage.GetMessageDistributionPattern().GetEnum<DistributionPattern>();
            EndOfFlowIdentity = multipartMessage.GetEndOfFlowIdentity();
            EndOfFlowReceiverIdentity = multipartMessage.GetEndOfFlowReceiverIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();
            CorrelationId = multipartMessage.GetCorrelationId();
        }

        public IMessage RegisterEndOfFlowReceiver(string endOfFlowMessageIdentity, string endOfFlowReceiverIdentity)
        {
            EndOfFlowReceiverIdentity = endOfFlowReceiverIdentity.GetBytes();
            EndOfFlowIdentity = endOfFlowMessageIdentity.GetBytes();

            if (Identity == endOfFlowMessageIdentity)
            {
                ReceiverIdentity = endOfFlowMessageIdentity.GetBytes();
            }

            return this;
        }

        internal Message RegisterEndOfFlowReceiver(byte[] endOfFlowMessageIdentity, byte[] endOfFlowReceiverIdentity)
        {
            EndOfFlowReceiverIdentity = endOfFlowReceiverIdentity;
            EndOfFlowIdentity = endOfFlowMessageIdentity;

            if (Unsafe.Equals(Identity.GetBytes(), endOfFlowMessageIdentity))
            {
                ReceiverIdentity = endOfFlowMessageIdentity;
            }

            return this;
        }

        internal Message SetCorrelationId(byte[] correlationId)
        {
            CorrelationId = correlationId;

            return this;
        }

        public T GetPayload<T>()
            where T : IPayload
            => (T) (payload ?? (payload = Deserialize<T>(Body)));

        private static byte[] Serialize(object payload)
            => messageSerializer.Serialize(payload);

        private static T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public byte[] Body { get; }
        public string Identity { get; }
        public string Version { get; }
        public TimeSpan TTL { get; set; }
        public DistributionPattern Distribution { get; }
        public byte[] CorrelationId { get; private set; }
        public byte[] ReceiverIdentity { get; private set; }
        public byte[] EndOfFlowIdentity { get; private set; }
        public byte[] EndOfFlowReceiverIdentity { get; private set; }
    }
}