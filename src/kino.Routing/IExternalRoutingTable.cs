using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public interface IExternalRoutingTable
    {
        PeerConnection AddMessageRoute(ExternalRouteDefinition routeDefinition);

        PeerConnection FindRoute(Identifier identifier, byte[] receiverNodeIdentity);

        PeerRemoveResult RemoveNodeRoute(SocketIdentifier socketIdentifier);

        PeerRemoveResult RemoveMessageRoute(IEnumerable<Identifier> identifiers, SocketIdentifier socketIdentifier);

        IEnumerable<PeerConnection> FindAllRoutes(Identifier identifier);

        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}