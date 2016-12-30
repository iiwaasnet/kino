using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Core;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class NodeRoutesRegistrar : INodeRoutesRegistrar
    {
        private readonly IClusterServices clusterServices;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public NodeRoutesRegistrar(IClusterServices clusterServices,
                                   IInternalRoutingTable internalRoutingTable,
                                   ISecurityProvider securityProvider)
        {
            this.clusterServices = clusterServices;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public void RegisterOwnGlobalRoutes(string domain)
        {
            var routes = internalRoutingTable.GetAllRoutes();
            var contracts = GetActorRoutes(domain, routes).Concat(GetMessageHubRoutes(routes))
                                                          .ToList();

            if (contracts.Any())
            {
                clusterServices.GetClusterMonitor().RegisterSelf(contracts, domain);
            }
        }

        private IEnumerable<Cluster.MessageRoute> GetActorRoutes(string domain, InternalRouting routes)
            => routes.Actors.SelectMany(r => r.Actors
                                              .Where(a => !a.LocalRegistration)
                                              .Select(a => new Cluster.MessageRoute
                                                           {
                                                               Receiver = new ReceiverIdentifier(a.Identity),
                                                               Message = r.Message
                                                           }))
                     .Where(r => securityProvider.GetDomain(r.Message.Identity) == domain);

        private static IEnumerable<Cluster.MessageRoute> GetMessageHubRoutes(InternalRouting routes)
            => routes.MessageHubs.Where(mh => !mh.LocalRegistration)
                     .Select(mh => new Cluster.MessageRoute
                                   {
                                       Receiver = new ReceiverIdentifier(mh.MessageHub.Identity)
                                   });
    }
}