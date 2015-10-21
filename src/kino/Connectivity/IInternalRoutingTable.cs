using System.Collections.Generic;
using kino.Messaging;

namespace kino.Connectivity
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier);
        SocketIdentifier FindRoute(IMessageIdentifier messageIdentifier);
        IEnumerable<SocketIdentifier> FindAllRoutes(IMessageIdentifier messageIdentifier);
        IEnumerable<IMessageIdentifier> GetMessageIdentifiers();
        IEnumerable<IMessageIdentifier> RemoveActorHostRoute(SocketIdentifier socketIdentifier);
        IEnumerable<InternalRoute> GetAllRoutes();
        bool CanRouteMessage(IMessageIdentifier messageIdentifier);
    }
}