using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Framework;

namespace kino.Messaging
{
    public class Message : IMessage
    {
        public static readonly byte[] CurrentVersion = "1.0".GetBytes();

        private object payload;
        private readonly MessageHops messageHops;
        private static readonly byte[] EmptyCorrelationId = Guid.Empty.ToString().GetBytes();

        private Message(IPayload payload, DistributionPattern distributionPattern)
        {
            messageHops = new MessageHops();
            Body = Serialize(payload);
            Version = payload.Version;
            Identity = payload.Identity;
            Distribution = distributionPattern;
            TTL = TimeSpan.Zero;
            TraceOptions = MessageTraceOptions.None;
        }

        public static IMessage CreateFlowStartMessage(IPayload payload)
            => new Message(payload, DistributionPattern.Unicast) {CorrelationId = GenerateCorrelationId()};

        public static IMessage Create(IPayload payload, DistributionPattern distributionPattern = DistributionPattern.Unicast)
            => new Message(payload, distributionPattern) {CorrelationId = EmptyCorrelationId};

        private static byte[] GenerateCorrelationId()
            //TODO: Better implementation
            => Guid.NewGuid().ToString().GetBytes();

        internal Message(MultipartMessage multipartMessage)
        {
            messageHops = new MessageHops(multipartMessage.GetMessageRoute());
            Body = multipartMessage.GetMessageBody();
            Identity = multipartMessage.GetMessageIdentity();
            Version = multipartMessage.GetMessageVersion();
            TTL = multipartMessage.GetMessageTTL().GetTimeSpan();
            Distribution = multipartMessage.GetMessageDistributionPattern().GetEnumFromInt<DistributionPattern>();
            CallbackIdentity = multipartMessage.GetCallbackIdentity();
            CallbackVersion = multipartMessage.GetCallbackVersion();
            CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();
            CorrelationId = multipartMessage.GetCorrelationId();
            TraceOptions = multipartMessage.GetTraceOptions().GetEnumFromLong<MessageTraceOptions>();
        }

        internal void RegisterCallbackPoint(byte[] callbackIdentity, byte[] callbackVersion, byte[] callbackReceiverIdentity)
        {
            CallbackReceiverIdentity = callbackReceiverIdentity;
            CallbackIdentity = callbackIdentity;
            CallbackVersion = callbackVersion;

            if (Unsafe.Equals(Identity, CallbackIdentity) && Unsafe.Equals(Version, CallbackVersion))
            {
                ReceiverIdentity = CallbackReceiverIdentity;
            }
        }

        internal void PushRouterAddress(SocketEndpoint scaleOutAddress)
            => messageHops.Add(scaleOutAddress);

        internal IEnumerable<SocketEndpoint> GetMessageHops()
            => messageHops.Hops;

        internal byte[] GetMessageHopsBytes()
            => messageHops.GetBytes();

        internal void CopyMessageHops(IEnumerable<SocketEndpoint> messageHops)
        {
            this.messageHops.Clear();
            this.messageHops.AddRange(messageHops);
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
        public byte[] CallbackVersion { get; private set; }
        public byte[] CallbackReceiverIdentity { get; private set; }
        public byte[] SocketIdentity { get; private set; }
        public MessageTraceOptions TraceOptions { get; set; }
    }
}