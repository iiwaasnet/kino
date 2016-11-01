using System;
using kino.Core;

namespace kino.Messaging
{
    public interface IMessage : IIdentifier
    {
        T GetPayload<T>() where T : IPayload, new();

        void SetReceiverNode(SocketIdentifier socketIdentifier);

        void EncryptPayload();

        void DecryptPayload();

        bool Equals(MessageIdentifier messageIdentifier);

        DistributionPattern Distribution { get; }

        byte[] CorrelationId { get; }

        TimeSpan TTL { get; set; }

        MessageTraceOptions TraceOptions { get; set; }

        byte[] Body { get; }

        ushort Hops { get; }

        string Domain { get; }
    }
}