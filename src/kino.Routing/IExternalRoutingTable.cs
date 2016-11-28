using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public interface IExternalRoutingTable
    {
        PeerConnection AddMessageRoute(ExternalRouteRegistration routeRegistration);

        IEnumerable<PeerConnection> FindRoutes(ExternalRouteLookupRequest lookupRequest);

        PeerRemoveResult RemoveNodeRoute(ReceiverIdentifier nodeIdentifier);

        PeerRemoveResult RemoveMessageRoute(ExternalRouteRemoval routeRemoval);
    }
}