using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class AsyncMessageContext
    {
        public IEnumerable<IMessage> OutMessages { get; internal set; }

        public byte[] CorrelationId { get; internal set; }

        public IEnumerable<MessageIdentifier> CallbackPoint { get; internal set; }

        public byte[] CallbackReceiverIdentity { get; internal set; }

        public long CallbackKey { get; internal set; }

        public IEnumerable<SocketEndpoint> MessageHops { get; internal set; }

        public MessageTraceOptions TraceOptions { get; set; }
    }
}