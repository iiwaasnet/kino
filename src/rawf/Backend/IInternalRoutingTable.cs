using System.Collections.Generic;

namespace rawf.Backend
{
    public interface IInternalRoutingTable
    {
        void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier);
        SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier);
        IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers();
    }
}