using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
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
        private readonly IClusterConnectivity clusterConnectivity;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public MessageRouteUnregistrationHandler(IClusterConnectivity clusterConnectivity,
                                                 IExternalRoutingTable externalRoutingTable,
                                                 ISecurityProvider securityProvider,
                                                 ILogger logger)
        {
            this.clusterConnectivity = clusterConnectivity;
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
                    var socketIdentifier = new ReceiverIdentifier(payload.SocketIdentity);
                    var messageContracts = payload.MessageContracts
                                                  .Select(mh => mh.ToIdentifier());
                    var messageIdentifiers = messageContracts.Where(mi => mi.IsMessageHub()
                                                                          || securityProvider.GetDomain(mi.Identity) == message.Domain);

                    var peerRemoveResult = externalRoutingTable.RemoveMessageRoute(messageIdentifiers, socketIdentifier);
                    if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
                    {
                        forwardingSocket.SafeDisconnect(peerRemoveResult.Uri);
                    }
                    if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
                    {
                        clusterConnectivity.DeletePeer(socketIdentifier);
                    }
                    LogIfMessagesBelongToOtherDomain(messageIdentifiers, messageContracts, message.Domain);
                }
            }

            return shouldHandle;
        }

        private void LogIfMessagesBelongToOtherDomain(IEnumerable<Identifier> messageIdentifiers,
                                                      IEnumerable<Identifier> messageContracts,
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