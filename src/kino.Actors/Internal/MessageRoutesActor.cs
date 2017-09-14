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
    public class MessageRoutesActor : Actor
    {
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IExternalRoutingTable externalRoutingTable;

        public MessageRoutesActor(IExternalRoutingTable externalRoutingTable,
                                  IInternalRoutingTable internalRoutingTable)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.internalRoutingTable = internalRoutingTable;
        }

        [MessageHandlerDefinition(typeof(RequestMessageExternalRoutesMessage), true)]
        public async Task<IActorResult> GetMessageExternalRoutes(IMessage message)
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
        public async Task<IActorResult> GetExternalRoutes(IMessage _)
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

        [MessageHandlerDefinition(typeof(RequestInternalRoutesMessage), true)]
        public async Task<IActorResult> GetInternalRoutes(IMessage _)
        {
            var routes = internalRoutingTable.GetAllRoutes();

            var response = new InternalRoutesMessage
                           {
                               MessageHubs = routes.MessageHubs
                                                   .Select(mh => new ReceiverRegistration
                                                                 {
                                                                     Identity = mh.MessageHub.Identity,
                                                                     LocalRegistration = mh.LocalRegistration
                                                                 })
                                                   .ToList(),
                               MessageRoutes = routes.Actors
                                                     .Select(mr => new MessageRegistration
                                                                   {
                                                                       Message = new MessageContract
                                                                                 {
                                                                                     Identity = mr.Message.Identity,
                                                                                     Partition = mr.Message.Partition,
                                                                                     Version = mr.Message.Version
                                                                                 },
                                                                       Actors = mr.Actors
                                                                                  .Select(a => a.Identity)
                                                                                  .ToList()
                                                                   })
                                                     .ToList()
                           };

            return new ActorResult(Message.Create(response));
        }
    }
}