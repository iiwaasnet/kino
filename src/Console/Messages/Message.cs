using System;
using Framework;

namespace Console.Messages
{
    public class Message : IMessage
    {
        public const string MessagesVersion = "1.0";
        private static readonly IMessageSerializer messageSerializer = new MessageSerializer();
        private object payload;

        private Message(IPayload payload, string messageIdentity)
        {
            Body = Serialize(payload);
            Version = MessagesVersion.GetBytes();
            Identity = messageIdentity.GetBytes();
            Distribution = DistributionPattern.Unicast;
            TTL = TimeSpan.Zero;
        }

        public static IMessage CreateFlowStartMessage(IPayload payload, string messageIdentity)
        {
            return new Message(payload, messageIdentity) {CorrelationId = GenerateCorrelationId()};
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
            Identity = multipartMessage.GetMessageIdentity();
            Version = multipartMessage.GetMessageVersion();
            TTL = multipartMessage.GetMessageTTL().GetTimeSpan();
            Distribution = multipartMessage.GetMessageDistributionPattern().GetEnum<DistributionPattern>();
            CallbackIdentity = multipartMessage.GetCallbackIdentity();
            CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();
            CorrelationId = multipartMessage.GetCorrelationId();
        }

        //public void RegisterCallbackPoint(ICallbackPoint callbackPoint)
        //{
        //    //CallbackReceiverIdentity = callbackPoint.ReceiverIdentity;
        //    CallbackIdentity = callbackPoint.MessageIdentity;

        //    if (Unsafe.Equals(Identity, CallbackIdentity))
        //    {
        //        ReceiverIdentity = CallbackReceiverIdentity;
        //    }
        //}

        internal void RegisterCallbackPoint(byte[] callbackIdentity, byte[] callbackReceiverIdentity)
        {
            CallbackReceiverIdentity = callbackReceiverIdentity;
            CallbackIdentity = callbackIdentity;

            if (Unsafe.Equals(Identity, CallbackIdentity))
            {
                ReceiverIdentity = CallbackReceiverIdentity;
            }
        }

        internal void SetCorrelationId(byte[] correlationId)
        {
            CorrelationId = correlationId;
        }

        public T GetPayload<T>()
            where T : IPayload
            => (T) (payload ?? (payload = Deserialize<T>(Body)));

        private static byte[] Serialize(object payload)
            => messageSerializer.Serialize(payload);

        private static T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public byte[] Body { get; }
        public byte[] Identity { get; }
        public byte[] Version { get; }
        public TimeSpan TTL { get; set; }
        public DistributionPattern Distribution { get; }
        public byte[] CorrelationId { get; private set; }
        public byte[] ReceiverIdentity { get; private set; }
        public byte[] CallbackIdentity { get; private set; }
        public byte[] CallbackReceiverIdentity { get; private set; }
    }
}