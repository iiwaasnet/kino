using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class ClusterMessageRoutesRequestHandler : IServiceMessageHandler
    {
        private readonly IClusterServices clusterServices;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public ClusterMessageRoutesRequestHandler(IClusterServices clusterServices,
                                                  IInternalRoutingTable internalRoutingTable,
                                                  ISecurityProvider securityProvider)
        {
            this.clusterServices = clusterServices;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket _)
        {
            var shouldHandle = IsRoutesRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var routes = internalRoutingTable.GetAllRoutes();
                    var contracts = GetActorRoutes(message, routes).Concat(GetMessageHubRoutes(routes))
                                                                   .ToList();

                    if (contracts.Any())
                    {
                        clusterServices.RegisterSelf(contracts, message.Domain);
                    }
                }
            }

            return shouldHandle;
        }

        private IEnumerable<Cluster.MessageRoute> GetActorRoutes(IMessage message, InternalRouting routes)
            => routes.Actors.SelectMany(r => r.Actors
                                              .Where(a => !a.LocalRegistration)
                                              .Select(a => new Cluster.MessageRoute
                                                           {
                                                               Receiver = new ReceiverIdentifier(a.Identity),
                                                               Message = r.Message
                                                           }))
                     .Where(r => securityProvider.GetDomain(r.Message.Identity) == message.Domain);

        private static IEnumerable<Cluster.MessageRoute> GetMessageHubRoutes(InternalRouting routes)
            => routes.MessageHubs.Where(mh => !mh.LocalRegistration)
                     .Select(mh => new Cluster.MessageRoute
                                   {
                                       Receiver = new ReceiverIdentifier(mh.MessageHub.Identity)
                                   });

        private static bool IsRoutesRequest(IMessage message)
            => message.Equals(KinoMessages.RequestClusterMessageRoutes);
    }
}