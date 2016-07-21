using System;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class UnreachableNodeUnregistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly IClusterMembership clusterMembership;

        public UnreachableNodeUnregistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                    IClusterMembership clusterMembership)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.clusterMembership = clusterMembership;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsUnregisterRouting(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<UnregisterUnreachableNodeMessage>();

                clusterMembership.DeleteClusterMember(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));
                var connectionAction = externalRoutingTable.RemoveNodeRoute(new SocketIdentifier(payload.SocketIdentity));
                if (connectionAction == PeerConnectionAction.Disconnect)
                {
                    forwardingSocket.SafeDisconnect(new Uri(payload.Uri));
                }
            }

            return shouldHandle;
        }

        private static bool IsUnregisterRouting(IMessage message)
            => message.Equals(KinoMessages.UnregisterUnreachableNode);
    }
}