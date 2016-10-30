using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(IdentityRegistration identityRegistration, SocketIdentifier socketIdentifier);

        SocketIdentifier FindRoute(Identifier identifier);

        IEnumerable<SocketIdentifier> FindAllRoutes(Identifier identifier);

        IEnumerable<IdentityRegistration> GetMessageRegistrations();

        IEnumerable<IdentityRegistration> RemoveActorHostRoute(SocketIdentifier socketIdentifier);

        IEnumerable<InternalRoute> GetAllRoutes();

        bool MessageHandlerRegisteredExternaly(Identifier identifier);
    }
}