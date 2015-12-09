using System.ComponentModel;
using System.Linq;
using kino.Core.Framework;
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
                //TODO: Add return value to check if the remote node should be disconnected
                externalRoutingTable.RemoveMessageRoute(payload
                                                            .MessageContracts
                                                            .Select(mh => new MessageIdentifier(mh.Version, mh.Identity)),
                                                        new SocketIdentifier(payload.SocketIdentity));
            }

            return shouldHandle;
        }

        private bool IsUnregisterMessageRouting(IMessage message)
            => Unsafe.Equals(UnregisterMessageRouteMessageIdentifier.Identity, message.Identity);
    }
}