using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using kino.Core;
using kino.Core.Framework;
using kino.Security;

namespace kino.Messaging
{
    public class Message : IMessage
    {
        public static readonly ushort CurrentVersion = 1;
        public static readonly string KinoMessageNamespace = "KINO";

        private object payload;
        private List<SocketEndpoint> routing;
        private static readonly byte[] EmptyCorrelationId = Guid.Empty.ToString().GetBytes();

        private Message(IPayload payload, string domain)
        {
            Domain = domain ?? string.Empty;
            WireFormatVersion = Versioning.CurrentWireFormatVersion;
            routing = new List<SocketEndpoint>();
            CallbackPoint = Enumerable.Empty<MessageIdentifier>();
            Body = Serialize(payload);
            Version = payload.Version;
            Identity = payload.Identity;
            Partition = payload.Partition;
            TTL = TimeSpan.Zero;
            Hops = 0;
            Signature = IdentityExtensions.Empty;
            TraceOptions = MessageTraceOptions.None;
            ReceiverNodeIdentity = IdentityExtensions.Empty;
            ReceiverIdentity = IdentityExtensions.Empty;
        }

        private Message()
        {
        }

        public static IMessage CreateFlowStartMessage(IPayload payload, string domain = null)
            => new Message(payload, domain)
               {
                   Distribution = DistributionPattern.Unicast,
                   CorrelationId = GenerateCorrelationId()
               };

        internal static IMessage Create(IPayload payload,
                                        DistributionPattern distributionPattern = DistributionPattern.Unicast)
            => new Message(payload, null)
               {
                   Distribution = distributionPattern,
                   CorrelationId = EmptyCorrelationId
               };

        public static IMessage Create(IPayload payload,
                                      string domain,
                                      DistributionPattern distributionPattern = DistributionPattern.Unicast)
            => new Message(payload, domain)
               {
                   Distribution = distributionPattern,
                   CorrelationId = EmptyCorrelationId
               };

        private static byte[] GenerateCorrelationId()
            //TODO: Better implementation
            => Guid.NewGuid().ToString().GetBytes();

        public bool Equals(MessageIdentifier messageIdentifier)
            => messageIdentifier.Equals(new MessageIdentifier(Identity, Version, Partition));

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
            Signature = multipartMessage.GetSignature();
            Domain = multipartMessage.GetDomain();

            MessageTraceOptions traceOptions;
            DistributionPattern distributionPattern;
            multipartMessage.GetTraceOptionsDistributionPattern(out traceOptions, out distributionPattern);
            TraceOptions = traceOptions;
            Distribution = distributionPattern;

            Identity = multipartMessage.GetMessageIdentity();
            Version = multipartMessage.GetMessageVersion().GetUShort();
            Partition = multipartMessage.GetMessagePartition();

            CallbackReceiverIdentity = multipartMessage.GetCallbackReceiverIdentity();
            CallbackReceiverNodeIdentity = multipartMessage.GetCallbackReceiverNodeIdentity();
            CallbackPoint = multipartMessage.GetCallbackPoints();
            CallbackKey = multipartMessage.GetCallbackKey();

            ReceiverNodeIdentity = multipartMessage.GetReceiverNodeIdentity();
            ReceiverIdentity = multipartMessage.GetReceiverIdentity();

            ushort hops;
            routing = new List<SocketEndpoint>(multipartMessage.GetMessageRouting(out hops));
            Hops = hops;
        }

        private void ReadWireFormatVersion(MultipartMessage multipartMessage)
            => WireFormatVersion = multipartMessage.GetWireFormatVersion().GetInt();

        internal void RegisterCallbackPoint(byte[] callbackReceiverNodeIdentity,
                                            byte[] callbackReceiverIdentity,
                                            MessageIdentifier callbackMessageIdentifier,
                                            long callbackKey)
            => RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                     callbackReceiverIdentity,
                                     new[] {callbackMessageIdentifier},
                                     callbackKey);

        internal void RegisterCallbackPoint(byte[] callbackReceiverNodeIdentity,
                                            byte[] callbackReceiverIdentity,
                                            IEnumerable<MessageIdentifier> callbackMessageIdentifiers,
                                            long callbackKey)
        {
            CallbackReceiverNodeIdentity = callbackReceiverNodeIdentity;
            CallbackReceiverIdentity = callbackReceiverIdentity;
            CallbackPoint = callbackMessageIdentifiers;
            CallbackKey = callbackKey;

            MatchMessageAgainstCallbackPoint();
        }

        private void MatchMessageAgainstCallbackPoint()
        {
            if (CallbackPoint.Any(identifier => Unsafe.ArraysEqual(Identity, identifier.Identity)
                                                && Version == identifier.Version
                                                && Unsafe.ArraysEqual(Partition, identifier.Partition)))
            {
                ReceiverIdentity = CallbackReceiverIdentity;
                ReceiverNodeIdentity = CallbackReceiverNodeIdentity;
            }
        }

        public void SetReceiverNode(ReceiverIdentifier receiverNode)
            => SetReceivers(receiverNode.Identity, IdentityExtensions.Empty);

        public void SetReceiverActor(ReceiverIdentifier receiverNode, ReceiverIdentifier receiverActor)
            => SetReceivers(receiverNode.Identity, receiverActor.Identity);

        private void SetReceivers(byte[] receiverNode, byte[] receiverActor)
        {
            if (Distribution == DistributionPattern.Broadcast)
            {
                throw new ArgumentException("Receiver node cannot be set for broadcast message!");
            }

            ReceiverIdentity = receiverActor;
            ReceiverNodeIdentity = receiverNode;
        }

        internal void PushRouterAddress(SocketEndpoint scaleOutAddress)
        {
            if (TraceOptions.HasFlag(MessageTraceOptions.Routing))
            {
                routing.Add(scaleOutAddress);
            }
        }

        internal void SignMessage(ISignatureProvider signatureProvider)
        {
            AssertDomainIsSet();

            Signature = signatureProvider.CreateSignature(Domain, GetSignatureFields());
        }

        internal void VerifySignature(ISignatureProvider signatureProvider)
        {
            var mac = signatureProvider.CreateSignature(Domain, GetSignatureFields());

            if (!Unsafe.ArraysEqual(Signature, mac))
            {
                throw new WrongMessageSignatureException();
            }
        }

        private void AssertDomainIsSet()
        {
            if (string.IsNullOrWhiteSpace(Domain))
            {
                throw new SecurityException($"{nameof(Domain)} is not set!");
            }
        }

        private byte[] GetSignatureFields()
        {
            var version = Version.GetBytes();
            var callbackReceiverIdentity = CallbackReceiverIdentity ?? IdentityExtensions.Empty;
            var capacity = Identity.Length
                           + version.Length
                           + Partition.Length
                           + Body.Length
                           + callbackReceiverIdentity.Length;
            using (var stream = new MemoryStream(capacity))
            {
                stream.Write(Identity, 0, Identity.Length);
                stream.Write(version, 0, version.Length);
                stream.Write(Partition, 0, Partition.Length);
                stream.Write(Body, 0, Body.Length);
                stream.Write(callbackReceiverIdentity, 0, callbackReceiverIdentity.Length);
                return stream.GetBuffer();
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

        public void EncryptPayload()
        {
            throw new NotImplementedException();
        }

        public void DecryptPayload()
        {
            throw new NotImplementedException();
        }

        //TODO: Think of deep cloning byte arrays
        internal IMessage Clone()
            => new Message
               {
                   Body = Body,
                   WireFormatVersion = WireFormatVersion,
                   Identity = Identity,
                   Partition = Partition,
                   Version = Version,
                   TTL = TTL,
                   CorrelationId = CorrelationId,
                   ReceiverIdentity = ReceiverIdentity,
                   ReceiverNodeIdentity = ReceiverNodeIdentity,
                   Signature = Signature,
                   //NOTE: CallbackPoint, if changed from immutable, should be deep-cloned here
                   CallbackPoint = CallbackPoint,
                   CallbackKey = CallbackKey,
                   CallbackReceiverIdentity = CallbackReceiverIdentity,
                   CallbackReceiverNodeIdentity = CallbackReceiverNodeIdentity,
                   SocketIdentity = SocketIdentity,
                   TraceOptions = TraceOptions,
                   Distribution = Distribution,
                   Hops = Hops,
                   Domain = Domain,
                   routing = new List<SocketEndpoint>(routing)
               };

        public override string ToString()
            => $"{nameof(Identity)}[{Identity?.GetAnyString()}]-" +
               $"{nameof(Version)}[{Version}]-" +
               $"{nameof(Partition)}[{Partition?.GetAnyString()}] " +
               $"{nameof(CorrelationId)}[{CorrelationId?.GetAnyString()}] " +
               $"{Distribution}";

        public byte[] Body { get; private set; }

        public byte[] Identity { get; private set; }

        public byte[] Partition { get; private set; }

        public ushort Version { get; private set; }

        public TimeSpan TTL { get; set; }

        public byte[] CorrelationId { get; private set; }

        public byte[] ReceiverIdentity { get; private set; }

        public byte[] ReceiverNodeIdentity { get; private set; }

        public byte[] Signature { get; private set; }

        public IEnumerable<MessageIdentifier> CallbackPoint { get; private set; }

        public long CallbackKey { get; private set; }

        public byte[] CallbackReceiverIdentity { get; private set; }

        public byte[] CallbackReceiverNodeIdentity { get; private set; }

        public byte[] SocketIdentity { get; private set; }

        public MessageTraceOptions TraceOptions { get; set; }

        public DistributionPattern Distribution { get; set; }

        public ushort Hops { get; private set; }

        public int WireFormatVersion { get; private set; }

        public string Domain { get; private set; }
    }
}