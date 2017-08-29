using System.Linq;
using System.Threading.Tasks;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using ExternalRoute = kino.Messaging.Messages.ExternalRoute;
using MessageContract = kino.Messaging.Messages.MessageContract;

namespace kino.Actors.Internal
{
    internal class MessageRoutesActor : Actor
    {
        private readonly IExternalRoutingTable externalRoutingTable;

        internal MessageRoutesActor(IExternalRoutingTable externalRoutingTable)
            => this.externalRoutingTable = externalRoutingTable;

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

        [MessageHandlerDefinition(typeof(RequestExternalRoutesMessage), true)]
        internal async Task<IActorResult> GetExternalRoutes(IMessage _)
        {
            var routes = externalRoutingTable.GetAllRoutes();

            var response = new ExternalRoutesMessage
                           {
                               Routes = routes.Select(r => new NodeExternalRegistration
                                                           {
                                                               NodeIdentity = r.Node.SocketIdentity,
                                                               NodeUri = r.Node.Uri.ToSocketAddress(),
                                                               MessageHubs = r.MessageHubs
                                                                              .Select(mh => mh.MessageHub.Identity)
                                                                              .ToList(),
                                                               MessageRoutes = r.MessageRoutes
                                                                                .Select(ar => new MessageRegistration
                                                                                              {
                                                                                                  Message = new MessageContract
                                                                                                            {
                                                                                                                Identity = ar.Message.Identity,
                                                                                                                Partition = ar.Message.Partition,
                                                                                                                Version = ar.Message.Version
                                                                                                            },
                                                                                                  Actors = ar.Actors
                                                                                                             .Select(a => a.Identity)
                                                                                                             .ToList()
                                                                                              })
                                                                                .ToList()
                                                           })
                                              .ToList()
                           };

            return new ActorResult(Message.Create(response));
        }
    }
}