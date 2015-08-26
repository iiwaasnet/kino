using System.Collections.Generic;
using rawf.Messaging;

namespace rawf.Connectivity
{
    public class AsyncMessageContext
    {
        public IMessage OutMessage { get; internal set; }
        public byte[] CorrelationId { get; internal set; }
        public byte[] CallbackIdentity { get; internal set; }
        public byte[] CallbackReceiverIdentity { get; internal set; }
        public IEnumerable<SocketEndpoint> MessageHops {get; internal set;}
    }
}