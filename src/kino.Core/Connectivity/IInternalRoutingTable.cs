using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(Identifier messageIdentifier, SocketIdentifier socketIdentifier);

        SocketIdentifier FindRoute(Identifier messageIdentifier);

        IEnumerable<SocketIdentifier> FindAllRoutes(Identifier messageIdentifier);

        IEnumerable<Identifier> GetMessageIdentifiers();

        IEnumerable<Identifier> RemoveActorHostRoute(SocketIdentifier socketIdentifier);

        IEnumerable<InternalRoute> GetAllRoutes();

        bool CanRouteMessage(Identifier messageIdentifier);
    }
}