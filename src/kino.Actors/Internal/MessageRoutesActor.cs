using System.Linq;
using System.Threading.Tasks;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using ExternalRoute = kino.Messaging.Messages.ExternalRoute;

namespace kino.Actors.Internal
{
    internal class MessageRoutesActor : Actor
    {
        private readonly IExternalRoutingTable externalRoutingTable;

        internal MessageRoutesActor(IExternalRoutingTable externalRoutingTable)
        {
            this.externalRoutingTable = externalRoutingTable;
        }

        [MessageHandlerDefinition(typeof(RequestMessageExternalRoutesMessage), true)]
        internal async Task<IActorResult> GetMessageExternalRoutes(IMessage message)
        {
            var messageContract = message.GetPayload<RequestMessageExternalRoutesMessage>().MessageContract;

            var routes = externalRoutingTable.FindAllActors(new MessageIdentifier(messageContract.Identity,
                                                                                  messageContract.Version,
                                                                                  messageContract.Partition));
            var response = new MessageExternalRoutesMessage
                           {
                               MessageContract = messageContract,
                               Routes = routes.Select(r => new ExternalRoute
                                                           {
                                                               NodeIdentity = r.NodeIdentifier.Identity,
                                                               ReceiverIdentity = r.Actors.Select(a => a.Identity)
                                                                                   .ToList()
                                                           })
                                              .ToArray()
                           };

            return new ActorResult(Message.Create(response));
        }
    }
}