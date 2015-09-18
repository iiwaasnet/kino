using System;
using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IExternalRoutingTable
    {
        void AddMessageRoute(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier, Uri uri);
        SocketIdentifier FindRoute(MessageIdentifier messageIdentifier);
        void RemoveNodeRoute(SocketIdentifier socketIdentifier);
        void RemoveMessageRoute(IEnumerable<MessageIdentifier> messageHandlerIdentifiers, SocketIdentifier socketIdentifier);
        IEnumerable<SocketIdentifier> FindAllRoutes(MessageIdentifier messageIdentifier);
    }
}