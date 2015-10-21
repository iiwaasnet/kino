using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;

namespace kino.Connectivity.ServiceMessageHandlers
{
    public class MessageRouteDiscoveryHandler : IServiceMessageHandler
    {
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly IClusterMonitor clusterMonitor;

        public MessageRouteDiscoveryHandler(IInternalRoutingTable internalRoutingTable, IClusterMonitor clusterMonitor)
        {
            this.internalRoutingTable = internalRoutingTable;
            this.clusterMonitor = clusterMonitor;
        }

        public bool Handle(IMessage message, ISocket scaleOutBackendSocket)
        {
            var shouldHandle = IsDiscoverMessageRouteRequest(message);
            if (shouldHandle)
            {
                var messageContract = message.GetPayload<DiscoverMessageRouteMessage>().MessageContract;
                var messageIdentifier = new MessageIdentifier(messageContract.Version, messageContract.Identity);
                if (internalRoutingTable.CanRouteMessage(messageIdentifier))
                {
                    clusterMonitor.RegisterSelf(new[] { messageIdentifier });
                }
            }

            return shouldHandle;
        }

        private bool IsDiscoverMessageRouteRequest(IMessage message)
           => Unsafe.Equals(DiscoverMessageRouteMessage.MessageIdentity, message.Identity);
    }
}