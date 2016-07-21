using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Diagnostics;
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
                    var messageGroups = UpdateLocalRoutingTable(handlerSocketIdentifier, payload.GlobalMessageContracts)
                        .Select(mh => new { Message = mh, SecurityDomain = securityProvider.GetSecurityDomain(mh.Identity)})
                        .GroupBy(mh => mh.SecurityDomain);

                    foreach (var group in messageGroups)
                    {
                        clusterMonitor.RegisterSelf(group.Select(g => g.Message), group.Key);
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