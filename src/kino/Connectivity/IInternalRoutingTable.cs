using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IInternalRoutingTable
    {
        void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier);
        SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier);
        IEnumerable<SocketIdentifier> PopAll(MessageHandlerIdentifier messageHandlerIdentifier);
        IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers();
        IEnumerable<MessageHandlerIdentifier> Remove(SocketIdentifier socketIdentifier);
    }
}