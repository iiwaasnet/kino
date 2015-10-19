using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Actors.Diagnostics
{
    public class KnownMessageRoutesActor : IActor
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly RouterConfiguration routerConfiguration;

        public KnownMessageRoutesActor(IExternalRoutingTable externalRoutingTable,
                                       IInternalRoutingTable internalRoutingTable,
                                       RouterConfiguration routerConfiguration)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.internalRoutingTable = internalRoutingTable;
            this.routerConfiguration = routerConfiguration;
        }

        [MessageHandlerDefinition(typeof(RequestKnownMessageRoutesMessage))]
        private async Task<IActorResult> Handler(IMessage message)
            => new ActorResult(Message.Create(new KnownMessageRoutesMessage
                                              {
                                                  ExternalRoutes = GetExternalRoutes(),
                                                  InternalRoutes = GetInternalRoutes()
                                              }));

        private MessageRoute GetInternalRoutes()
            => new MessageRoute
               {
                   SocketIdentity = routerConfiguration.ScaleOutAddress.Identity,
                   Uri = routerConfiguration.ScaleOutAddress.Uri.AbsoluteUri,
                   MessageContracts = internalRoutingTable
                       .GetAllRoutes()
                       .SelectMany(ir => ir.Messages)
                       .Select(m => new MessageContract
                                    {
                                        Version = m.Version,
                                        Identity = m.Identity
                                    })
                       .ToArray()
               };

        private IEnumerable<MessageRoute> GetExternalRoutes()
            => externalRoutingTable
                .GetAllRoutes()
                .Select(mr => new MessageRoute
                              {
                                  SocketIdentity = mr.Node.SocketIdentity,
                                  Uri = mr.Node.Uri.ToSocketAddress(),
                                  MessageContracts = mr.Messages
                                                       .Select(m => new MessageContract
                                                                    {
                                                                        Version = m.Version,
                                                                        Identity = m.Identity
                                                                    })
                                                       .ToArray()
                              });

        //public IEnumerable<MessageHandlerDefinition> GetInterfaceDefinition()
        //{
        //    yield return new MessageHandlerDefinition
        //                 {
        //                     Handler = Handler,
        //                     Message = new MessageDefinition
        //                               {
        //                                   Identity = RequestKnownMessageRoutesMessage.MessageIdentity,
        //                                   Version = Message.CurrentVersion
        //                               }
        //                 };
        //}
    }
}