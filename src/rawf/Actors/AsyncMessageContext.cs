using rawf.Messaging;

namespace rawf.Actors
{
    internal class AsyncMessageContext
    {
        internal IMessage OutMessage { get; set; }
        internal byte[] CorrelationId { get; set; }
        internal byte[] CallbackIdentity { get; set; }
        internal byte[] CallbackReceiverIdentity { get; set; }
    }
}