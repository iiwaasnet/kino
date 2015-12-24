using System;
using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Core.Messaging
{
    public interface IMessage
    {
        T GetPayload<T>() where T : IPayload, new();

        bool Equals(MessageIdentifier messageIdentifier);

        DistributionPattern Distribution { get; }

        byte[] Version { get; }

        byte[] Identity { get; }

        byte[] CorrelationId { get; }

        byte[] ReceiverIdentity { get; }

        byte[] CallbackReceiverIdentity { get; }

        IEnumerable<MessageIdentifier> CallbackPoint { get; }

        TimeSpan TTL { get; set; }

        MessageTraceOptions TraceOptions { get; set; }

        byte[] Body { get; }
    }
}