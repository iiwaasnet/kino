using System;
using System.Collections.Generic;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;

namespace kino.Connectivity.ServiceMessageHandlers
{
    public class InternalMessageRouteRegistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ILogger logger;

        public InternalMessageRouteRegistrationHandler(IClusterMonitor clusterMonitor,
                                                       IInternalRoutingTable internalRoutingTable,
                                                       ILogger logger)
        {
            this.clusterMonitor = clusterMonitor;
            this.internalRoutingTable = internalRoutingTable;
            this.logger = logger;
        }

        public bool Handle(IMessage message, ISocket scaleOutBackendSocket)
        {
            var shouldHandle = IsInternalMessageRoutingRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterInternalMessageRouteMessage>();
                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

                var handlers = UpdateLocalRoutingTable(payload, handlerSocketIdentifier);

                clusterMonitor.RegisterSelf(handlers);
            }

            return shouldHandle;
        }

        private static bool IsInternalMessageRoutingRegistration(IMessage message)
            => Unsafe.Equals(RegisterInternalMessageRouteMessage.MessageIdentity, message.Identity);

        private IEnumerable<MessageIdentifier> UpdateLocalRoutingTable(RegisterInternalMessageRouteMessage payload,
                                                                        SocketIdentifier socketIdentifier)
        {
            var handlers = new List<MessageIdentifier>();

            foreach (var registration in payload.MessageContracts)
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