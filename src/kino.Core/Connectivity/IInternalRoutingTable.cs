using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IInternalRoutingTable
    {
        void AddMessageRoute(IdentityRegistration identityRegistration, ILocalSendingSocket<IMessage> receivingSocket);

        ILocalSendingSocket<IMessage> FindRoute(Identifier identifier);

        IEnumerable<ILocalSendingSocket<IMessage>> FindAllRoutes(Identifier identifier);

        IEnumerable<IdentityRegistration> GetMessageRegistrations();

        IEnumerable<IdentityRegistration> RemoveActorHostRoute(ILocalSendingSocket<IMessage> socketIdentifier);

        IEnumerable<InternalRoute> GetAllRoutes();

        bool MessageHandlerRegisteredExternaly(Identifier identifier);
    }
}