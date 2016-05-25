using System;
using kino.Core.Connectivity;

namespace kino.Core.Messaging
{
    public interface IMessage : IMessageIdentifier
    {
        T GetPayload<T>() where T : IPayload, new();

        void SetReceiverNode(SocketIdentifier socketIdentifier);

        bool Equals(MessageIdentifier messageIdentifier);

        DistributionPattern Distribution { get; }

        byte[] CorrelationId { get; }

        TimeSpan TTL { get; set; }

        MessageTraceOptions TraceOptions { get; set; }

        byte[] Body { get; }

        ushort Hops { get; }
    }
}