using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class InternalMessageRouteRegistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public InternalMessageRouteRegistrationHandler(IClusterMonitorProvider clusterMonitorProvider,
                                                       IInternalRoutingTable internalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       ILogger logger)
        {
            clusterMonitor = clusterMonitorProvider.GetClusterMonitor();
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsInternalMessageRoutingRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterInternalMessageRouteMessage>();
                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

                if (payload.LocalMessageContracts != null)
                {
                    UpdateLocalRoutingTable(handlerSocketIdentifier, payload.LocalMessageContracts);
                }
                if (payload.GlobalMessageContracts != null)
                {
                    var newRoutes = UpdateLocalRoutingTable(handlerSocketIdentifier, payload.GlobalMessageContracts);
                    var messageHubs = newRoutes.Where(mi => mi.IsMessageHub())
                                               .ToList();
                    var messageGroups = newRoutes.Where(mi => !mi.IsMessageHub())
                                                 .Select(mh => new {Message = mh, Domain = securityProvider.GetDomain(mh.Identity)})
                                                 .GroupBy(mh => mh.Domain)
                                                 .ToList();

                    foreach (var domain in securityProvider.GetAllowedDomains())
                    {
                        var contracts = messageGroups.Where(g => g.Key == domain)
                                                     .SelectMany(g => g.Select(_ => _.Message))
                                                     .Concat(messageHubs);
                        if (contracts.Any())
                        {
                            clusterMonitor.RegisterSelf(contracts, domain);
                        }
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsInternalMessageRoutingRegistration(IMessage message)
            => message.Equals(KinoMessages.RegisterInternalMessageRoute);

        private IEnumerable<MessageIdentifier> UpdateLocalRoutingTable(SocketIdentifier socketIdentifier, MessageContract[] messageContracts)
        {
            var handlers = new List<MessageIdentifier>();

            foreach (var registration in messageContracts)
            {
                try
                {
                    var messageIdentifier = new MessageIdentifier(registration.Version,
                                                                  registration.Identity,
                                                                  registration.Partition);
                    internalRoutingTable.AddMessageRoute(messageIdentifier, socketIdentifier);
                    handlers.Add(messageIdentifier);
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