using System;
using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Routing
{
    public interface IExternalRoutingTable
    {
        PeerConnection AddMessageRoute(Identifier messageIdentifier, SocketIdentifier socketIdentifier, Uri uri);

        PeerConnection FindRoute(Identifier messageIdentifier, byte[] receiverNodeIdentity);

        PeerConnectionAction RemoveNodeRoute(SocketIdentifier socketIdentifier);

        PeerConnectionAction RemoveMessageRoute(IEnumerable<Identifier> messageHandlerIdentifiers, SocketIdentifier socketIdentifier);

        IEnumerable<PeerConnection> FindAllRoutes(Identifier messageIdentifier);

        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}