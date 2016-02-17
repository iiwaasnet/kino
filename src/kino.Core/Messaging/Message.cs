using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Framework;

namespace kino.Core.Messaging
{
    public class Message : IMessage
    {
        public static readonly byte[] CurrentVersion = "1.0".GetBytes();
        public static readonly string KinoMessageNamespace = "KINO";

        private object payload;
        private readonly List<SocketEndpoint> hops;
        private static readonly byte[] EmptyCorrelationId = Guid.Empty.ToString().GetBytes();

        private Message(IPayload payload, DistributionPattern distributionPattern)
        {
            hops = new List<SocketEndpoint>();
            CallbackPoint = Enumerable.Empty<MessageIdentifier>();
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

        public bool Equals(MessageIdentifier messageIdentifier)
            => messageIdentifier.Equals(new MessageIdentifier(Version, Identity));

        internal Message(MultipartMessage multipartMessage)
        {
            hops = new List<SocketEndpoint>(multipartMessage.GetMessageHops());
            Body = multipartMessage.GetMessageBody();
            Identity = multipartMessage.GetMessageIdentity();
            Version = multipartMessage.GetMessageVersion();
            TTL = multipartMessage.GetMessageTTL().GetTimeSpan();
            Distribution = multipartMessage.GetMessageDistributionPattern().GetEnumFromInt<DistributionPattern>();
            CallbackPoint = multipartMessage.GetCallbackPoints();
            CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();
            CorrelationId = multipartMessage.GetCorrelationId();
            TraceOptions = multipartMessage.GetTraceOptions().GetEnumFromLong<MessageTraceOptions>();
        }

        internal void RegisterCallbackPoint(byte[] callbackReceiverIdentity, MessageIdentifier callbackMessageIdentifier)
        {
            RegisterCallbackPoint(callbackReceiverIdentity, new[] {callbackMessageIdentifier});
        }

        internal void RegisterCallbackPoint(byte[] callbackReceiverIdentity, IEnumerable<MessageIdentifier> callbackMessageIdentifiers)
        {
            CallbackReceiverIdentity = callbackReceiverIdentity;
            CallbackPoint = callbackMessageIdentifiers;

            if (CallbackPoint.Any(identifier => Unsafe.Equals(Identity, identifier.Identity) && Unsafe.Equals(Version, identifier.Version)))
            {
                ReceiverIdentity = CallbackReceiverIdentity;
            }
        }

        internal void PushRouterAddress(SocketEndpoint scaleOutAddress)
        {
            if (TraceOptions == MessageTraceOptions.Routing)
            {
                hops.Add(scaleOutAddress);
            }
        }

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

        public IEnumerable<MessageIdentifier> CallbackPoint { get; private set; }

        public byte[] CallbackReceiverIdentity { get; private set; }

        public byte[] SocketIdentity { get; private set; }

        public MessageTraceOptions TraceOptions { get; set; }
    }
}