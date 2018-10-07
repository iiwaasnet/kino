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
    public class Message : MessageIdentifier, IMessage
    {
        public static readonly ushort CurrentVersion = 1;
        public static readonly string KinoMessageNamespace = "KINO";

        private object payload;
        private List<SocketEndpoint> routing;
        private List<MessageIdentifier> callbackPoint;
        private static readonly byte[] EmptyCorrelationId = Guid.Empty.ToString().GetBytes();

        internal Message(byte[] identity, ushort version, byte[] partition)
            : base(identity, version, partition)
        {
            Domain = string.Empty;
            WireFormatVersion = Versioning.CurrentWireFormatVersion;
            routing = new List<SocketEndpoint>();
            callbackPoint = new List<MessageIdentifier>();
            TTL = TimeSpan.Zero;
            Hops = 0;
            Signature = IdentityExtensions.Empty;
            TraceOptions = MessageTraceOptions.None;
            ReceiverNodeIdentity = IdentityExtensions.Empty;
            ReceiverIdentity = IdentityExtensions.Empty;
        }

        private Message(IPayload payload)
            : this(payload.Identity, payload.Version, payload.Partition)
            => Body = Serialize(payload);

        public static IMessage CreateFlowStartMessage(IPayload payload, byte[] correlationId = null)
            => new Message(payload)
               {
                   Distribution = DistributionPattern.Unicast,
                   CorrelationId = correlationId ?? GenerateCorrelationId()
               };

        public static IMessage Create(IPayload payload,
                                      DistributionPattern distributionPattern = DistributionPattern.Unicast,
                                      byte[] correlationId = null)
            => new Message(payload)
               {
                   Distribution = distributionPattern,
                   CorrelationId = correlationId ?? EmptyCorrelationId
               };

        private static byte[] GenerateCorrelationId()
            //TODO: Better implementation
            => Guid.NewGuid().ToString().GetBytes();

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

        internal bool RemoveCallbackPoint(MessageIdentifier callbackPoint)
            => this.callbackPoint.Remove(callbackPoint);

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
            => routing.Add(scaleOutAddress);

        internal void SignMessage(ISignatureProvider signatureProvider)
        {
            if (signatureProvider.ShouldSignMessage(Domain, Identity))
            {
                AssertDomainIsSet();

                Signature = signatureProvider.CreateSignature(Domain, GetSignatureFields());
            }
        }

        internal void VerifySignature(ISignatureProvider signatureProvider)
        {
            if (signatureProvider.ShouldSignMessage(Domain, Identity))
            {
                var mac = signatureProvider.CreateSignature(Domain, GetSignatureFields());

                if (!Unsafe.ArraysEqual(Signature, mac))
                {
                    throw new WrongMessageSignatureException();
                }
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

        internal void CopyCallbackPoint(IEnumerable<MessageIdentifier> callbackPoints)
        {
            callbackPoint.Clear();
            callbackPoint.AddRange(callbackPoints);
        }

        internal void SetCorrelationId(byte[] correlationId)
            => CorrelationId = correlationId;

        internal void SetReceiverIdentity(byte[] receiverIdentity)
            => ReceiverIdentity = receiverIdentity;

        internal void SetSocketIdentity(byte[] socketIdentity)
            => SocketIdentity = socketIdentity;

        internal void SetReceiverNodeIdentity(byte[] receiverNodeIdentity)
            => ReceiverNodeIdentity = receiverNodeIdentity;

        internal void SetSignature(byte[] signature)
            => Signature = signature;

        internal void SetBody(byte[] body)
            => Body = body;

        internal void SetCallbackKey(long callbackKey)
            => CallbackKey = callbackKey;

        internal void SetCallbackReceiverIdentity(byte[] callbackReceiverIdentity)
            => CallbackReceiverIdentity = callbackReceiverIdentity;

        internal void SetCallbackReceiverNodeIdentity(byte[] callbackReceiverNodeIdentity)
            => CallbackReceiverNodeIdentity = callbackReceiverNodeIdentity;

        internal void SetDistribution(DistributionPattern distribution)
            => Distribution = distribution;

        internal void SetHops(ushort hops)
            => Hops = hops;

        internal void SetWireFormatVersion(ushort wireFormatVersion)
            => WireFormatVersion = wireFormatVersion;

        internal void SetDomain(string domain)
            => Domain = domain ?? string.Empty;

        public T GetPayload<T>()
            where T : IPayload, new()
            => (T) (payload ?? (payload = Deserialize<T>(Body)));

        private byte[] Serialize(IPayload payload)
            => payload.Serialize();

        private T Deserialize<T>(byte[] content)
            where T : IPayload, new()
            => new T().Deserialize<T>(content);

        public void EncryptPayload()
            => throw new NotImplementedException();

        public void DecryptPayload()
            => throw new NotImplementedException();

        //TODO: Think of deep cloning byte arrays
        internal Message Clone()
            => new Message(Identity, Version, Partition)
               {
                   Body = Body,
                   WireFormatVersion = WireFormatVersion,
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

        public TimeSpan TTL { get; set; }

        public byte[] CorrelationId { get; private set; }

        public byte[] ReceiverIdentity { get; private set; }

        public byte[] ReceiverNodeIdentity { get; private set; }

        public byte[] Signature { get; private set; }

        public IEnumerable<MessageIdentifier> CallbackPoint
        {
            get => callbackPoint;
            private set => callbackPoint = value.ToList();
        }

        public long CallbackKey { get; private set; }

        public byte[] CallbackReceiverIdentity { get; private set; }

        public byte[] CallbackReceiverNodeIdentity { get; private set; }

        public byte[] SocketIdentity { get; private set; }

        public MessageTraceOptions TraceOptions { get; set; }

        public DistributionPattern Distribution { get; private set; }

        public ushort Hops { get; private set; }

        public ushort WireFormatVersion { get; private set; }

        public string Domain { get; private set; }
    }
}