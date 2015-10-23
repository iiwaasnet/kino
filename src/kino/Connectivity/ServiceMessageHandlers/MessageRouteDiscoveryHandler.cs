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
        private static readonly MessageIdentifier DiscoverMessageRouteMessageIdentifier = MessageIdentifier.Create<DiscoverMessageRouteMessage>();

        public MessageRouteDiscoveryHandler(IInternalRoutingTable internalRoutingTable, IClusterMonitor clusterMonitor)
        {
            this.internalRoutingTable = internalRoutingTable;
            this.clusterMonitor = clusterMonitor;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
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
           => Unsafe.Equals(DiscoverMessageRouteMessageIdentifier.Identity, message.Identity);
    }
}