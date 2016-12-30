using System.Collections.Generic;
using kino.Connectivity;
using kino.Messaging;

namespace kino.Routing
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(InternalRouteRegistration routeRegistration);

        IEnumerable<ILocalSendingSocket<IMessage>> FindRoutes(InternalRouteLookupRequest lookupRequest);

        IEnumerable<MessageRoute> RemoveReceiverRoute(ILocalSendingSocket<IMessage> socketIdentifier);

        InternalRouting GetAllRoutes();
    }
}