using System.Linq;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class RoutesRegistrationRequestHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private static readonly MessageIdentifier RequestClusterMessageRoutesMessageIdentifier = MessageIdentifier.Create<RequestClusterMessageRoutesMessage>();
        private static readonly MessageIdentifier RequestNodeMessageRoutesMessageIdentifier = MessageIdentifier.Create<RequestNodeMessageRoutesMessage>();

        public RoutesRegistrationRequestHandler(IClusterMonitor clusterMonitor, IInternalRoutingTable internalRoutingTable)
        {
            this.clusterMonitor = clusterMonitor;
            this.internalRoutingTable = internalRoutingTable;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
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
            => message.Equals(RequestClusterMessageRoutesMessageIdentifier)
               || message.Equals(RequestNodeMessageRoutesMessageIdentifier);
    }
}