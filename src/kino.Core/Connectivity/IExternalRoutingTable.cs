using System;
using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public interface IExternalRoutingTable
    {
        void AddMessageRoute(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier, Uri uri);
        PeerConnection FindRoute(MessageIdentifier messageIdentifier);
        PeerConnectionAction RemoveNodeRoute(SocketIdentifier socketIdentifier);
        PeerConnectionAction RemoveMessageRoute(IEnumerable<MessageIdentifier> messageHandlerIdentifiers, SocketIdentifier socketIdentifier);
        IEnumerable<PeerConnection> FindAllRoutes(MessageIdentifier messageIdentifier);
        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}