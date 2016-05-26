using System;
using System.Linq;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class MessageRouteUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private static readonly MessageIdentifier UnregisterMessageRouteMessageIdentifier = MessageIdentifier.Create<UnregisterMessageRouteMessage>();

        public MessageRouteUnregistrationHandler(IExternalRoutingTable externalRoutingTable)
        {
            this.externalRoutingTable = externalRoutingTable;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterMessageRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                var connectionAction = externalRoutingTable.RemoveMessageRoute(payload
                                                                                   .MessageContracts
                                                                                   .Select(mh => new MessageIdentifier(mh.Version,
                                                                                                                       mh.Identity,
                                                                                                                       mh.Partition)),
                                                                               new SocketIdentifier(payload.SocketIdentity));
                if (connectionAction == PeerConnectionAction.Disconnect)
                {
                    forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                }
            }

            return shouldHandle;
        }

        private bool IsUnregisterMessageRouting(IMessage message)
            => message.Equals(UnregisterMessageRouteMessageIdentifier);
    }
}