using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class ClusterMessageRoutesRequestHandler : IServiceMessageHandler
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public ClusterMessageRoutesRequestHandler(IClusterMonitorProvider clusterMonitorProvider,
                                                  IInternalRoutingTable internalRoutingTable,
                                                  ISecurityProvider securityProvider)
        {
            clusterMonitor = clusterMonitorProvider.GetClusterMonitor();
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsRoutesRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var messageIdentifiers = internalRoutingTable.GetMessageRegistrations();
                    var messageHubs = messageIdentifiers.Where(mi => mi.Identifier.IsMessageHub())
                                                        .Select(mi => mi.Identifier);
                    //TODO: Refactor, hence !mi.IsMessageHub() should be first condition
                    var contracts = messageIdentifiers.Where(mi => !mi.Identifier.IsMessageHub() &&
                                                                   securityProvider.GetDomain(mi.Identifier.Identity) == message.Domain)
                                                      .Select(mi => mi.Identifier)
                                                      .Concat(messageHubs)
                                                      .ToList();

                    if (contracts.Any())
                    {
                        clusterMonitor.RegisterSelf(contracts, message.Domain);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsRoutesRequest(IMessage message)
            => message.Equals(KinoMessages.RequestClusterMessageRoutes);
    }
}