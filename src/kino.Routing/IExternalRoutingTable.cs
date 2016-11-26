using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public interface IExternalRoutingTable
    {
        PeerConnection AddMessageRoute(ExternalRouteRegistration routeRegistration);

        //PeerConnection FindRoute(Identifier identifier, byte[] receiverNodeIdentity);

        IEnumerable<PeerConnection> FindRoutes(ExternalRouteLookupRequest lookupRequest);

        PeerRemoveResult RemoveNodeRoute(ReceiverIdentifier socketIdentifier);

        PeerRemoveResult RemoveMessageRoute(ExternalRouteRemoval routeRemoval);

        IEnumerable<PeerConnection> FindAllRoutes(Identifier identifier);

        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}