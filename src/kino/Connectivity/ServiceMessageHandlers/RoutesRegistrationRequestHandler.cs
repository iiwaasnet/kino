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
            => Unsafe.Equals(RequestClusterMessageRoutesMessageIdentifier.Identity, message.Identity)
               || Unsafe.Equals(RequestNodeMessageRoutesMessageIdentifier.Identity, message.Identity);
    }
}