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
    public class MessageRouteUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public MessageRouteUnregistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                 ISecurityProvider securityProvider,
                                                 ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
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
                    var messageContracts = payload.MessageContracts
                                                  .Select(mh => new MessageIdentifier(mh.Version,
                                                                                      mh.Identity,
                                                                                      mh.Partition));
                    var messageIdentifiers = messageContracts
                        .Where(mi => securityProvider.GetSecurityDomain(mi.Identity) == message.SecurityDomain);
                    var connectionAction = externalRoutingTable.RemoveMessageRoute(messageIdentifiers,
                                                                                   new SocketIdentifier(payload.SocketIdentity));
                    if (connectionAction == PeerConnectionAction.Disconnect)
                    {
                        forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                    }
                    LogIfMessagesBelongToOtherDomain(messageIdentifiers, messageContracts, message.SecurityDomain);
                }
            }

            return shouldHandle;
        }

        private void LogIfMessagesBelongToOtherDomain(IEnumerable<MessageIdentifier> messageIdentifiers,
                                                      IEnumerable<MessageIdentifier> messageContracts,
                                                      string securityDomain)
        {
            foreach (var messageContract in messageContracts.Where(mc => !messageIdentifiers.Contains(mc)))
            {
                logger.Warn($"MessageIdentity {messageContract.Identity.GetString()} doesn't belong to requested " +
                            $"SecurityDomain {securityDomain}!");
            }
        }

        private bool IsUnregisterMessageRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterMessageRoute);
    }
}