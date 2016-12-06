using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class InternalMessageRouteRegistrationHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public InternalMessageRouteRegistrationHandler(IClusterMonitor clusterMonitor,
                                                       IInternalRoutingTable internalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       ILogger logger)
        {
            this.clusterMonitor = clusterMonitor;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public void Handle(InternalRouteRegistration routeRegistration)
        {
            if (routeRegistration.ReceiverIdentifier.IsMessageHub()
                || routeRegistration.ReceiverIdentifier.IsActor() && routeRegistration.MessageContracts?.Any() == true)
            {
                internalRoutingTable.AddMessageRoute(routeRegistration);
                var routesByDomain = GetActors(routeRegistration).Concat(GetMessageHubs(routeRegistration))
                                                                 .GroupBy(mh => mh.Domain);
                foreach (var route in routesByDomain)
                {
                    clusterMonitor.RegisterSelf(route.Select(r => r.MessageRoute), route.Key);
                }
            }
        }

        private IEnumerable<MessageRouteDomainMap> GetActors(InternalRouteRegistration routeRegistration)
            => routeRegistration.ToEnumerable()
                                .Where(r => !r.KeepRegistrationLocal && r.ReceiverIdentifier.IsActor())
                                .SelectMany(a => a.MessageContracts.Select(mc => new MessageRouteDomainMap
                                                                                 {
                                                                                     MessageRoute = new Cluster.MessageRoute
                                                                                                    {
                                                                                                        Receiver = a.ReceiverIdentifier,
                                                                                                        Message = mc.Message
                                                                                                    },
                                                                                     Domain = securityProvider.GetDomain(mc.Message.Identity)
                                                                                 }));

        private IEnumerable<MessageRouteDomainMap> GetMessageHubs(InternalRouteRegistration routeRegistration)
            => routeRegistration.ToEnumerable()
                                .Where(r => !r.KeepRegistrationLocal && r.ReceiverIdentifier.IsMessageHub())
                                .SelectMany(r => securityProvider.GetAllowedDomains()
                                                                 .Select(dom => new MessageRouteDomainMap
                                                                                {
                                                                                    MessageRoute = new Cluster.MessageRoute
                                                                                                   {
                                                                                                       Receiver = r.ReceiverIdentifier
                                                                                                   },
                                                                                    Domain = dom
                                                                                }));
    }
}