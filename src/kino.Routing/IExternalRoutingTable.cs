using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public interface IExternalRoutingTable
    {
        PeerConnection AddMessageRoute(ExternalRouteRegistration routeRegistration);

        PeerConnection FindRoute(Identifier identifier, byte[] receiverNodeIdentity);

        PeerRemoveResult RemoveNodeRoute(ReceiverIdentifier socketIdentifier);

        PeerRemoveResult RemoveMessageRoute(IEnumerable<Identifier> identifiers, ReceiverIdentifier socketIdentifier);

        IEnumerable<PeerConnection> FindAllRoutes(Identifier identifier);

        IEnumerable<ExternalRoute> GetAllRoutes();
    }
}