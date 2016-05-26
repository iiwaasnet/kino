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
        private List<SocketEndpoint> routing;
        private byte[] receiverNodeIdentity;
        private static readonly byte[] EmptyCorrelationId = Guid.Empty.ToString().GetBytes();

        private Message(IPayload payload, DistributionPattern distribution)
        {
            WireFormatVersion = Versioning.CurrentWireFormatVersion;
            routing = new List<SocketEndpoint>();
            CallbackPoint = Enumerable.Empty<MessageIdentifier>();
            Body = Serialize(payload);
            Version = payload.Version;
            Identity = payload.Identity;
            Partition = payload.Partition;
            Distribution = distribution;
            TTL = TimeSpan.Zero;
            Hops = 0;
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
            => messageIdentifier.Equals(new MessageIdentifier(Version, Identity, Partition));

        internal Message(MultipartMessage multipartMessage)
        {
            ReadWireFormatVersion(multipartMessage);
            ReadV4Frames(multipartMessage);
        }

        private void ReadV4Frames(MultipartMessage multipartMessage)
        {
            Body = multipartMessage.GetMessageBody();
            TTL = multipartMessage.GetMessageTTL();
            CorrelationId = multipartMessage.GetCorrelationId();

            MessageTraceOptions traceOptions;
            DistributionPattern distributionPattern;
            multipartMessage.GetTraceOptionsDistributionPattern(out traceOptions, out distributionPattern);
            TraceOptions = traceOptions;
            Distribution = distributionPattern;

            Identity = multipartMessage.GetMessageIdentity();
            Version = multipartMessage.GetMessageVersion();
            Partition = multipartMessage.GetMessagePartition();
            receiverNodeIdentity = multipartMessage.GetReceiverNodeIdentity();
            CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();
            CallbackPoint = multipartMessage.GetCallbackPoints(WireFormatVersion);

            ushort hops;
            routing = new List<SocketEndpoint>(multipartMessage.GetMessageRouting(out hops));
            Hops = hops;
        }

        private void ReadWireFormatVersion(MultipartMessage multipartMessage)
        {
            WireFormatVersion = multipartMessage.GetWireFormatVersion().GetInt();
        }

        internal void RegisterCallbackPoint(byte[] callbackReceiverIdentity, MessageIdentifier callbackMessageIdentifier)
        {
            RegisterCallbackPoint(callbackReceiverIdentity, new[] {callbackMessageIdentifier});
        }

        internal void RegisterCallbackPoint(byte[] callbackReceiverIdentity, IEnumerable<MessageIdentifier> callbackMessageIdentifiers)
        {
            CallbackReceiverIdentity = callbackReceiverIdentity;
            CallbackPoint = callbackMessageIdentifiers;

            if (CallbackPoint.Any(identifier => Unsafe.Equals(Identity, identifier.Identity)
                                                && Unsafe.Equals(Version, identifier.Version)
                                                && Unsafe.Equals(Partition, identifier.Partition)))
            {
                ReceiverIdentity = CallbackReceiverIdentity;
            }
        }

        public void SetReceiverNode(SocketIdentifier socketIdentifier)
        {
            if (Distribution == DistributionPattern.Broadcast)
            {
                throw new ArgumentException("Receiver node cannot be set for broadcast message!");
            }

            receiverNodeIdentity = socketIdentifier.Identity;
        }

        internal byte[] PopReceiverNode()
        {
            var tmp = receiverNodeIdentity;
            receiverNodeIdentity = null;

            return tmp;
        }

        internal bool ReceiverNodeSet()
            => receiverNodeIdentity.IsSet();

        internal void PushRouterAddress(SocketEndpoint scaleOutAddress)
        {
            if (TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                routing.Add(scaleOutAddress);
            }
        }

        internal void AddHop()
            => Hops++;

        internal IEnumerable<SocketEndpoint> GetMessageRouting()
            => routing;

        internal void CopyMessageRouting(IEnumerable<SocketEndpoint> messageRouting)
        {
            routing.Clear();
            routing.AddRange(messageRouting);
        }

        internal void SetCorrelationId(byte[] correlationId)
            => CorrelationId = correlationId;

        internal void SetSocketIdentity(byte[] socketIdentity)
            => SocketIdentity = socketIdentity;

        public T GetPayload<T>()
            where T : IPayload, new()
            => (T) (payload ?? (payload = Deserialize<T>(Body)));

        private byte[] Serialize(IPayload payload)
            => payload.Serialize();

        private T Deserialize<T>(byte[] content)
            where T : IPayload, new()
            => new T().Deserialize<T>(content);

        public override string ToString()
            => $"{nameof(Identity)}[{Identity?.GetAnyString()}]-" +
               $"{nameof(Version)}[{Version?.GetAnyString()}]-" +
               $"{nameof(Partition)}[{Partition?.GetAnyString()}] " +
               $"{nameof(CorrelationId)}[{CorrelationId?.GetAnyString()}] " +
               $"{Distribution}";

        public byte[] Body { get; private set; }

        public byte[] Identity { get; private set; }

        public byte[] Partition { get; private set; }

        public byte[] Version { get; private set; }

        public TimeSpan TTL { get; set; }

        public byte[] CorrelationId { get; private set; }

        public byte[] ReceiverIdentity { get; private set; }

        public IEnumerable<MessageIdentifier> CallbackPoint { get; private set; }

        public byte[] CallbackReceiverIdentity { get; private set; }

        public byte[] SocketIdentity { get; private set; }

        public MessageTraceOptions TraceOptions { get; set; }

        public DistributionPattern Distribution { get; set; }

        public ushort Hops { get; private set; }

        public int WireFormatVersion { get; private set; }
    }
}