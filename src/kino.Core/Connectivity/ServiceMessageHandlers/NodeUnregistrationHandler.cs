using System;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class NodeUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IClusterMembership clusterMembership;
        private readonly ISecurityProvider securityProvider;

        public NodeUnregistrationHandler(IExternalRoutingTable externalRoutingTable,
                                         IClusterMembership clusterMembership,
                                         ISecurityProvider securityProvider)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.clusterMembership = clusterMembership;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                if (securityProvider.SecurityDomainIsAllowed(message.SecurityDomain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var payload = message.GetPayload<UnregisterNodeMessage>();

                    clusterMembership.DeleteClusterMember(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
                    var connectionAction = externalRoutingTable.RemoveNodeRoute(new SocketIdentifier(payload.SocketIdentity));
                    if (connectionAction == PeerConnectionAction.Disconnect)
                    {
                        forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsUnregisterRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterNode);
    }
}