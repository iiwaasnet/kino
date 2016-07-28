using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Security;

namespace kino.Core.Messaging
{
    public class Message : IMessage
    {
        public static readonly byte[] CurrentVersion = "1.0".GetBytes();
        public static readonly string KinoMessageNamespace = "KINO";
        private const int DefaultMACBufferSize = 2 * 1024;

        private object payload;
        private List<SocketEndpoint> routing;
        private byte[] receiverNodeIdentity;
        private static readonly byte[] EmptyCorrelationId = Guid.Empty.ToString().GetBytes();

        private Message(IPayload payload, string securityDomain)
        {
            SecurityDomain = securityDomain ?? string.Empty;
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
        }

        public static IMessage CreateFlowStartMessage(IPayload payload, string securityDomain = null)
            => new Message(payload, securityDomain)
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
                                      string securityDomain,
                                      DistributionPattern distributionPattern = DistributionPattern.Unicast)
            => new Message(payload, securityDomain)
               {
                   Distribution = distributionPattern,
                   CorrelationId = EmptyCorrelationId
               };

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
            Signature = multipartMessage.GetSignature();
            SecurityDomain = multipartMessage.GetSecurityDomain();

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

        internal void SignMessage(ISignatureProvider signatureProvider)
        {
            AssertSecurityDomainIsSet();

            Signature = signatureProvider.CreateSignature(SecurityDomain, GetSignatureFields());
        }

        internal void VerifySignature(ISignatureProvider signatureProvider)
        {
            var mac = signatureProvider.CreateSignature(SecurityDomain, GetSignatureFields());

            if (!Unsafe.Equals(Signature, mac))
            {
                throw new WrongMessageSignatureException();
            }
        }

        private void AssertSecurityDomainIsSet()
        {
            if (string.IsNullOrWhiteSpace(SecurityDomain))
            {
                throw new SecurityException($"{nameof(SecurityDomain)} is not set!");
            }
        }

        private byte[] GetSignatureFields()
        {
            using (var stream = new MemoryStream(DefaultMACBufferSize))
            {
                stream.Write(Identity, 0, Identity.Length);
                stream.Write(Version, 0, Version.Length);
                stream.Write(Partition, 0, Partition.Length);
                stream.Write(Body, 0, Body.Length);
                var callbackReceiverIdentity = CallbackReceiverIdentity ?? IdentityExtensions.Empty;
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

        public byte[] Signature { get; private set; }

        public IEnumerable<MessageIdentifier> CallbackPoint { get; private set; }

        public byte[] CallbackReceiverIdentity { get; private set; }

        public byte[] SocketIdentity { get; private set; }

        public MessageTraceOptions TraceOptions { get; set; }

        public DistributionPattern Distribution { get; set; }

        public ushort Hops { get; private set; }

        public int WireFormatVersion { get; private set; }

        public string SecurityDomain { get; private set; }
    }
}