using System;
using System.Collections.Generic;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;
using NetMQ;

namespace kino.Connectivity.ServiceMessageHandlers
{
    public class RouteUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;

        public RouteUnregistrationHandler(IExternalRoutingTable externalRoutingTable)
        {
            this.externalRoutingTable = externalRoutingTable;
        }

        public bool Handle(IMessage message, ISocket scaleOutBackendSocket)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterNodeMessageRouteMessage>();
                externalRoutingTable.RemoveNodeRoute(new SocketIdentifier(payload.SocketIdentity));
                try
                {
                    scaleOutBackendSocket.Disconnect(new Uri(payload.Uri));
                }
                catch (EndpointNotFoundException)
                {
                }
            }

            return shouldHandle;
        }

        private bool IsUnregisterRouting(IMessage message)
            => Unsafe.Equals(UnregisterNodeMessageRouteMessage.MessageIdentity, message.Identity);
    }
}