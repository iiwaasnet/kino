using System;
using System.Collections.Generic;
using kino.Messaging;

namespace kino.Connectivity
{
    public interface IExternalRoutingTable
    {
        void AddMessageRoute(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier, Uri uri);
        Node FindRoute(IMessageIdentifier messageIdentifier);
        void RemoveNodeRoute(SocketIdentifier socketIdentifier);
        void RemoveMessageRoute(IEnumerable<IMessageIdentifier> messageHandlerIdentifiers, SocketIdentifier socketIdentifier);
        IEnumerable<Node> FindAllRoutes(IMessageIdentifier messageIdentifier);
        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}