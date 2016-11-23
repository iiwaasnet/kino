using System.Collections.Generic;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;

namespace kino.Routing
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(InternalRouteRegistration routeRegistration);

        //void AddMessageRoute(IdentityRegistration identityRegistration, ILocalSendingSocket<IMessage> receivingSocket);

        //ILocalSendingSocket<IMessage> FindRoute(Identifier identifier);

        IEnumerable<ILocalSendingSocket<IMessage>> FindRoutes(InternalRouteLookupRequest lookupRequest);

        //IEnumerable<ILocalSendingSocket<IMessage>> FindAllRoutes(Identifier identifier);

        //IEnumerable<IdentityRegistration> GetMessageRegistrations();

        IEnumerable<MessageRoute> RemoveActorHostRoute(ILocalSendingSocket<IMessage> socketIdentifier);

        InternalRouting GetAllRoutes();

        bool MessageHandlerRegisteredExternaly(Identifier identifier);
    }
}