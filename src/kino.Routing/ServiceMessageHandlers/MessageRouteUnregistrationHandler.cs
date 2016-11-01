using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
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
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                    var messageContracts = payload.MessageContracts
                                                  .Select(mh => new MessageIdentifier(mh.Identity,
                                                                                      mh.Version, mh.Partition));
                    var messageIdentifiers = messageContracts.Where(mi => securityProvider.GetDomain(mi.Identity) == message.Domain);
                    var connectionAction = externalRoutingTable.RemoveMessageRoute(messageIdentifiers,
                                                                                   new SocketIdentifier(payload.SocketIdentity));
                    if (connectionAction == PeerConnectionAction.Disconnect)
                    {
                        forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                    }
                    LogIfMessagesBelongToOtherDomain(messageIdentifiers, messageContracts, message.Domain);
                }
            }

            return shouldHandle;
        }

        private void LogIfMessagesBelongToOtherDomain(IEnumerable<MessageIdentifier> messageIdentifiers,
                                                      IEnumerable<MessageIdentifier> messageContracts,
                                                      string domain)
        {
            foreach (var messageContract in messageContracts.Where(mc => !messageIdentifiers.Contains(mc)))
            {
                logger.Warn($"MessageIdentity {messageContract.Identity.GetString()} doesn't belong to requested " +
                            $"Domain {domain}!");
            }
        }

        private bool IsUnregisterMessageRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterMessageRoute);
    }
}