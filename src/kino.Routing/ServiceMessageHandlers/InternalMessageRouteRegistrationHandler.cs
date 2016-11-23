using System;
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
        private readonly IClusterServices clusterServices;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public InternalMessageRouteRegistrationHandler(IClusterServices clusterServices,
                                                       IInternalRoutingTable internalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       ILogger logger)
        {
            this.clusterServices = clusterServices;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public void Handle(InternalRouteRegistration routeRegistration)
        {
            if (routeRegistration.MessageContracts != null)
            {
                var newGlobalRoutes = UpdateLocalRoutingTable(routeRegistration);
                var messageGroups = GetMessageHandlers(newGlobalRoutes).Concat(GetMessageHubs(newGlobalRoutes))
                                                                       .GroupBy(mh => mh.Domain);
                foreach (var group in messageGroups)
                {
                    clusterServices.RegisterSelf(group.Select(g => g.Identity).ToList(), group.Key);
                }
            }
        }

        private IEnumerable<IdentityDomainMap> GetMessageHandlers(IEnumerable<Identifier> newRoutes)
            => newRoutes.Where(mi => !mi.IsMessageHub())
                        .Select(mh => new IdentityDomainMap
                                      {
                                          Identity = mh,
                                          Domain = securityProvider.GetDomain(mh.Identity)
                                      });

        private IEnumerable<IdentityDomainMap> GetMessageHubs(IEnumerable<Identifier> newRoutes)
            => newRoutes.Where(mi => mi.IsMessageHub())
                        .SelectMany(mi => securityProvider.GetAllowedDomains()
                                                          .Select(dom => new IdentityDomainMap
                                                                         {
                                                                             Identity = mi,
                                                                             Domain = dom
                                                                         }));

        private IEnumerable<Identifier> UpdateLocalRoutingTable(InternalRouteRegistration routeRegistration)
        {
            var handlers = new List<Identifier>();
            internalRoutingTable.AddMessageRoute(routeRegistration);

            foreach (var registration in routeRegistration.MessageContracts)
            {
                try
                {
                    internalRoutingTable.AddMessageRoute(routeRegistration);
                    //internalRoutingTable.AddMessageRoute(new IdentityRegistration(registration.MessageIdentifier, registration.KeepRegistrationLocal),
                    //                                     routeRegistration.DestinationSocket);
                    if (!registration.KeepRegistrationLocal)
                    {
                        handlers.Add(registration.Message);
                    }
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }

            return handlers;
        }
    }
}