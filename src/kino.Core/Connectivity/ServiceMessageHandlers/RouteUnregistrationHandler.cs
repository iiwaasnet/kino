using System;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class RouteUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private static readonly MessageIdentifier UnregisterNodeMessageRouteMessageIdentifier = MessageIdentifier.Create<UnregisterNodeMessageRouteMessage>();

        public RouteUnregistrationHandler(IExternalRoutingTable externalRoutingTable)
        {
            this.externalRoutingTable = externalRoutingTable;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterNodeMessageRouteMessage>();
                var connectionAction = externalRoutingTable.RemoveNodeRoute(new SocketIdentifier(payload.SocketIdentity));
                if (connectionAction == PeerConnectionAction.Disconnect)
                {
                    forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                }
            }

            return shouldHandle;
        }

        private static bool IsUnregisterRouting(IMessage message)
            => message.Equals(UnregisterNodeMessageRouteMessageIdentifier);
    }
}