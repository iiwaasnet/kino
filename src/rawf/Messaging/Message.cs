using System;
using System.Collections.Generic;
using rawf.Connectivity;
using rawf.Framework;

namespace rawf.Messaging
{
    public class Message : IMessage
    {
        public static readonly byte[] CurrentVersion = "1.0".GetBytes();

        private object payload;
        private readonly List<SocketEndpoint> hops;

        private Message(IPayload payload, byte[] messageIdentity, DistributionPattern distributionPattern)
        {
            hops = new List<SocketEndpoint>();
            Body = Serialize(payload);
            Version = CurrentVersion;
            Identity = messageIdentity;
            Distribution = distributionPattern;
            TTL = TimeSpan.Zero;
            TraceOptions = MessageTraceOptions.None;
        }

        public static IMessage CreateFlowStartMessage(IPayload payload, byte[] messageIdentity)
            => new Message(payload, messageIdentity, DistributionPattern.Unicast) {CorrelationId = GenerateCorrelationId()};

        public static IMessage Create(IPayload payload, byte[] messageIdentity, DistributionPattern distributionPattern = DistributionPattern.Unicast)
            => new Message(payload, messageIdentity, distributionPattern);

        private static byte[] GenerateCorrelationId()
            //TODO: Better implementation
            => Guid.NewGuid().ToString().GetBytes();

        internal Message(MultipartMessage multipartMessage)
        {
            hops = new List<SocketEndpoint>(multipartMessage.GetMessageHops());
            Body = multipartMessage.GetMessageBody();
            Identity = multipartMessage.GetMessageIdentity();
            Version = multipartMessage.GetMessageVersion();
            TTL = multipartMessage.GetMessageTTL().GetTimeSpan();
            Distribution = multipartMessage.GetMessageDistributionPattern().GetEnumFromInt<DistributionPattern>();
            CallbackIdentity = multipartMessage.GetCallbackIdentity();
            CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();
            CorrelationId = multipartMessage.GetCorrelationId();
            TraceOptions = multipartMessage.GetTraceOptions().GetEnumFromLong<MessageTraceOptions>();
        }

        internal void RegisterCallbackPoint(byte[] callbackIdentity, byte[] callbackReceiverIdentity)
        {
            CallbackReceiverIdentity = callbackReceiverIdentity;
            CallbackIdentity = callbackIdentity;

            if (Unsafe.Equals(Identity, CallbackIdentity))
            {
                ReceiverIdentity = CallbackReceiverIdentity;
            }
        }

        internal void PushRouterAddress(SocketEndpoint scaleOutAddress)
            => hops.Add(scaleOutAddress);

        internal IEnumerable<SocketEndpoint> GetMessageHops()
            => hops;
            
        internal void CopyMessageHops(IEnumerable<SocketEndpoint> messageHops)
        {
            hops.Clear();
            hops.AddRange(messageHops);
        }        

        internal void SetCorrelationId(byte[] correlationId)
            => CorrelationId = correlationId;

        internal void SetSocketIdentity(byte[] socketIdentity)
            => SocketIdentity = socketIdentity;

        internal string GetIdentityString()
            => Identity.GetString();

        public T GetPayload<T>()
            where T : IPayload, new()
            => (T) (payload ?? (payload = Deserialize<T>(Body)));

        private byte[] Serialize(IPayload payload)
            => payload.Serialize();

        private T Deserialize<T>(byte[] content)
            where T : IPayload, new()
            => new T().Deserialize<T>(content);

        public byte[] Body { get; }
        public byte[] Identity { get; }
        public byte[] Version { get; }
        public TimeSpan TTL { get; set; }
        public DistributionPattern Distribution { get; }
        public byte[] CorrelationId { get; private set; }
        public byte[] ReceiverIdentity { get; private set; }
        public byte[] CallbackIdentity { get; private set; }
        public byte[] CallbackReceiverIdentity { get; private set; }
        public byte[] SocketIdentity { get; private set; }
        public MessageTraceOptions TraceOptions { get; set; }
    }
}