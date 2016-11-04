using System;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class NodeUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IClusterConnectivity clusterConnectivity;
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public NodeUnregistrationHandler(IClusterConnectivity clusterConnectivity,
                                         IExternalRoutingTable externalRoutingTable,
                                         ISecurityProvider securityProvider)
        {
            this.clusterConnectivity = clusterConnectivity;
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var payload = message.GetPayload<UnregisterNodeMessage>();

                    var socketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                    var peerRemoveResult = externalRoutingTable.RemoveNodeRoute(socketIdentifier);
                    if (peerRemoveResult.ConnectionAction == PeerConnectionAction.Disconnect)
                    {
                        forwardingSocket.SafeDisconnect(peerRemoveResult.Uri);
                    }
                    if (peerRemoveResult.ConnectionAction != PeerConnectionAction.KeepConnection)
                    {
                        clusterConnectivity.DeletePeer(socketIdentifier);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsUnregisterRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterNode);
    }
}