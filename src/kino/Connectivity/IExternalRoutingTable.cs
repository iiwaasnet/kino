using System;
using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IExternalRoutingTable
    {
        void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier, Uri uri);
        SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier);
        void RemoveRoute(SocketIdentifier socketIdentifier);
        void RemoveMessageRoute(IEnumerable<MessageHandlerIdentifier> messageHandlerIdentifiers, SocketIdentifier socketIdentifier);
        IEnumerable<SocketIdentifier> PopAll(MessageHandlerIdentifier messageHandlerIdentifier);
    }
}