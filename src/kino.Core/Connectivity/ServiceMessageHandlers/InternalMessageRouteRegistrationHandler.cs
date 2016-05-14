using System;
using System.Collections.Generic;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class InternalMessageRouteRegistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ILogger logger;
        private static readonly MessageIdentifier RegisterInternalMessageRouteMessageIdentifier = MessageIdentifier.Create<RegisterInternalMessageRouteMessage>();

        public InternalMessageRouteRegistrationHandler(IClusterMonitorProvider clusterMonitorProvider,
                                                       IInternalRoutingTable internalRoutingTable,
                                                       ILogger logger)
        {
            clusterMonitor = clusterMonitorProvider.GetClusterMonitor();
            this.internalRoutingTable = internalRoutingTable;
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
                    var globalHandlers = UpdateLocalRoutingTable(handlerSocketIdentifier, payload.GlobalMessageContracts);
                    clusterMonitor.RegisterSelf(globalHandlers);
                }
            }

            return shouldHandle;
        }

        private static bool IsInternalMessageRoutingRegistration(IMessage message)
            => message.Equals(RegisterInternalMessageRouteMessageIdentifier);

        private IEnumerable<MessageIdentifier> UpdateLocalRoutingTable(SocketIdentifier socketIdentifier, MessageContract[] messageContracts)
        {
            var handlers = new List<MessageIdentifier>();

            foreach (var registration in messageContracts)
            {
                try
                {
                    var messageIdentifier = new MessageIdentifier(registration.Version, registration.Identity);
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