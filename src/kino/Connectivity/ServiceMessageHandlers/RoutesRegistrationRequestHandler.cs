using System.Linq;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;

namespace kino.Connectivity.ServiceMessageHandlers
{
    public class RoutesRegistrationRequestHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;

        public RoutesRegistrationRequestHandler(IClusterMonitor clusterMonitor, IInternalRoutingTable internalRoutingTable)
        {
            this.clusterMonitor = clusterMonitor;
            this.internalRoutingTable = internalRoutingTable;
        }

        public bool Handle(IMessage message, ISocket scaleOutBackendSocket)
        {
            var shouldHandle = IsRoutesRequest(message);
            if (shouldHandle)
            {
                var messageIdentifiers = internalRoutingTable.GetMessageIdentifiers();
                if (messageIdentifiers.Any())
                {
                    clusterMonitor.RegisterSelf(messageIdentifiers);
                }
            }

            return shouldHandle;
        }

        private static bool IsRoutesRequest(IMessage message)
            => Unsafe.Equals(RequestClusterMessageRoutesMessage.MessageIdentity, message.Identity)
               || Unsafe.Equals(RequestNodeMessageRoutesMessage.MessageIdentity, message.Identity);
    }
}