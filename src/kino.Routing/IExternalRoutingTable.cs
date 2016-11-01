using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public interface IExternalRoutingTable
    {
        PeerConnection AddMessageRoute(ExternalRouteDefinition routeDefinition);

        PeerConnection FindRoute(Identifier messageIdentifier, byte[] receiverNodeIdentity);

        PeerConnectionAction RemoveNodeRoute(SocketIdentifier socketIdentifier);

        PeerConnectionAction RemoveMessageRoute(IEnumerable<Identifier> messageHandlerIdentifiers, SocketIdentifier socketIdentifier);

        IEnumerable<PeerConnection> FindAllRoutes(Identifier messageIdentifier);

        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}