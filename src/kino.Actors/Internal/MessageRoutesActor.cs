using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using MessageContract = kino.Messaging.Messages.MessageContract;

namespace kino.Actors.Internal
{
    public class MessageRoutesActor : Actor
    {
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly IExternalRoutingTable externalRoutingTable;

        public MessageRoutesActor(IExternalRoutingTable externalRoutingTable,
                                  IInternalRoutingTable internalRoutingTable,
                                  IScaleOutConfigurationProvider scaleOutConfigurationProvider)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.internalRoutingTable = internalRoutingTable;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
        }

        [MessageHandlerDefinition(typeof(RequestMessageRoutesMessage), true)]
        public async Task<IActorResult> GetMessageExternalRoutes(IMessage message)
        {
            var payload = message.GetPayload<RequestMessageRoutesMessage>();
            var messageContract = payload.MessageContract;
            var messageIdentifier = new MessageIdentifier(messageContract.Identity,
                                                          messageContract.Version,
                                                          messageContract.Partition);

            var externalRoutes = GetExternalRoutes();
            var internalRoutes = GetInternalRoutes();
            var localScaleOutSocket = scaleOutConfigurationProvider.GetScaleOutAddress();

            var response = new MessageRoutesMessage
                           {
                               MessageContract = messageContract,
                               ExternalRoutes = externalRoutes.Select(r => new MessageReceivingRoute
                                                                           {
                                                                               NodeIdentity = r.NodeIdentifier.Identity,
                                                                               ActorIdentity = r.Actors
                                                                                                .Select(a => a.Identity)
                                                                                                .ToList()
                                                                           })
                                                              .ToArray(),
                               InternalRoutes = new MessageReceivingRoute
                                                {
                                                    NodeIdentity = localScaleOutSocket.Identity,
                                                    ActorIdentity = internalRoutes.Select(r => r.Identity)
                                                                                  .ToList()
                                                }
                           };

            return new ActorResult(Message.Create(response));

            IEnumerable<NodeActors> GetExternalRoutes()
                => (payload.RouteType != RouteType.Internal)
                       ? externalRoutingTable.FindAllActors(messageIdentifier)
                       : Enumerable.Empty<NodeActors>();

            IEnumerable<ReceiverIdentifierRegistration> GetInternalRoutes()
                => (payload.RouteType != RouteType.External)
                       ? internalRoutingTable.GetAllRoutes()
                                             .Actors
                                             .Where(a => a.Message.Equals(messageIdentifier))
                                             .SelectMany(a => a.Actors)
                       : Enumerable.Empty<ReceiverIdentifierRegistration>();
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
                                                               NodeUri = r.Node.Uri,
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