using rawf.Messaging;

namespace rawf.Backend
{
    public class AsyncMessageContext
    {
        public IMessage OutMessage { get; internal set; }
        public byte[] CorrelationId { get; internal set; }
        public byte[] CallbackIdentity { get; internal set; }
        public byte[] CallbackReceiverIdentity { get; internal set; }
    }
}