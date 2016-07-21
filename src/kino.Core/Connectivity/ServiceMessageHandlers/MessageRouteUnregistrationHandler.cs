using System;
using System.Linq;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class MessageRouteUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public MessageRouteUnregistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                 ISecurityProvider securityProvider)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterMessageRouting(message);
            if (shouldHandle)
            {
                if (securityProvider.SecurityDomainIsAllowed(message.SecurityDomain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                    //TODO: Check if messages belong to same Domain as stated
                    var messageIdentifiers = payload.MessageContracts
                                                    .Select(mh => new MessageIdentifier(mh.Version,
                                                                                        mh.Identity,
                                                                                        mh.Partition));
                    var connectionAction = externalRoutingTable.RemoveMessageRoute(messageIdentifiers,
                                                                                   new SocketIdentifier(payload.SocketIdentity));
                    if (connectionAction == PeerConnectionAction.Disconnect)
                    {
                        forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                    }
                }
            }

            return shouldHandle;
        }

        private bool IsUnregisterMessageRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterMessageRoute);
    }
}