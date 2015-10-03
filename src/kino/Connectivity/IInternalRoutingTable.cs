using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier);
        SocketIdentifier FindRoute(MessageIdentifier messageIdentifier);
        IEnumerable<SocketIdentifier> FindAllRoutes(MessageIdentifier messageIdentifier);
        IEnumerable<MessageIdentifier> GetMessageIdentifiers();
        IEnumerable<MessageIdentifier> RemoveActorHostRoute(SocketIdentifier socketIdentifier);
        IEnumerable<InternalRoute> GetAllRoutes();
    }
}