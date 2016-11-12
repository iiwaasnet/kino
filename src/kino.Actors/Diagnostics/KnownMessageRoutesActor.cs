using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;
using MessageContract = kino.Messaging.Messages.MessageContract;

namespace kino.Actors.Diagnostics
{
    public class KnownMessageRoutesActor : Actor
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly ISecurityProvider securityProvider;
        private static readonly MessageIdentifier KnownMessageRoutes = MessageIdentifier.Create<KnownMessageRoutesMessage>();

        public KnownMessageRoutesActor(IExternalRoutingTable externalRoutingTable,
                                       IInternalRoutingTable internalRoutingTable,
                                       IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                                       ISecurityProvider securityProvider)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.internalRoutingTable = internalRoutingTable;
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.securityProvider = securityProvider;
        }

        [MessageHandlerDefinition(typeof(RequestKnownMessageRoutesMessage))]
        private async Task<IActorResult> Handler(IMessage message)
            => new ActorResult(Message.Create(new KnownMessageRoutesMessage
                                              {
                                                  ExternalRoutes = GetExternalRoutes(),
                                                  InternalRoutes = GetInternalRoutes()
                                              },
                                              securityProvider.GetDomain(KnownMessageRoutes.Identity)));

        private MessageRoute GetInternalRoutes()
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            return new MessageRoute
                   {
                       SocketIdentity = scaleOutAddress.Identity,
                       Uri = scaleOutAddress.Uri.AbsoluteUri,
                       MessageContracts = internalRoutingTable
                           .GetAllRoutes()
                           .SelectMany(ir => ir.Messages)
                           .Select(m => new MessageContract
                                        {
                                            Version = m.Version,
                                            Identity = m.Identity,
                                            Partition = m.Partition,
                                            IsAnyIdentifier = m is AnyIdentifier
                                        })
                           .ToArray()
                   };
        }

        private IEnumerable<MessageRoute> GetExternalRoutes()
            => externalRoutingTable
                .GetAllRoutes()
                .Select(mr => new MessageRoute
                              {
                                  SocketIdentity = mr.Connection.Node.SocketIdentity,
                                  Uri = mr.Connection.Node.Uri.ToSocketAddress(),
                                  Connected = mr.Connection.Connected,
                                  MessageContracts = mr.Messages
                                                       .Select(m => new MessageContract
                                                                    {
                                                                        Version = m.Version,
                                                                        Identity = m.Identity,
                                                                        Partition = m.Partition,
                                                                        IsAnyIdentifier = m is AnyIdentifier
                                                                    })
                                                       .ToArray()
                              });
    }
}