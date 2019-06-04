using System.Collections.Generic;
using kino.Core;
using kino.Messaging;

namespace kino.Actors
{
    public class AsyncMessageContext
    {
        public IEnumerable<IMessage> OutMessages { get; internal set; }

        public byte[] CorrelationId { get; internal set; }

        public IEnumerable<MessageIdentifier> CallbackPoint { get; internal set; }

        public byte[] CallbackReceiverIdentity { get; internal set; }

        public byte[] CallbackReceiverNodeIdentity { get; internal set; }

        public long CallbackKey { get; internal set; }

        public IEnumerable<NodeAddress> MessageHops { get; internal set; }

        public MessageTraceOptions TraceOptions { get; set; }
    }
}