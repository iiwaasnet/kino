using System;
using kino.Core;

namespace kino.Messaging
{
    public interface IMessage : IIdentifier, IEquatable<IIdentifier>
    {
        T GetPayload<T>() where T : IPayload, new();

        void SetReceiverNode(ReceiverIdentifier receiverNode);

        void SetReceiverActor(ReceiverIdentifier receiverNode, ReceiverIdentifier receiverActor);

        void EncryptPayload();

        void DecryptPayload();

        DistributionPattern Distribution { get; }

        byte[] CorrelationId { get; }

        TimeSpan TTL { get; set; }

        MessageTraceOptions TraceOptions { get; set; }

        byte[] Body { get; }

        ushort Hops { get; }

        string Domain { get; }
    }
}