using System.Linq;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class MessageRouteDiscoveryHandler : IServiceMessageHandler
    {
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;
        private readonly IClusterMonitor clusterMonitor;

        public MessageRouteDiscoveryHandler(IClusterMonitorProvider clusterMonitorProvider,
                                            IInternalRoutingTable internalRoutingTable,
                                            ISecurityProvider securityProvider,
                                            ILogger logger)
        {
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
            clusterMonitor = clusterMonitorProvider.GetClusterMonitor();
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsDiscoverMessageRouteRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var messageContract = message.GetPayload<DiscoverMessageRouteMessage>().MessageContract;
                    var messageIdentifier = new MessageIdentifier(messageContract.Identity,
                                                                  messageContract.Version, messageContract.Partition);
                    if (internalRoutingTable.CanRouteMessage(messageIdentifier))
                    {
                        var domains = messageIdentifier.IsMessageHub()
                                                  ? securityProvider.GetAllowedDomains()
                                                  : new[] {securityProvider.GetDomain(messageIdentifier.Identity)};
                        if (domains.Contains(message.Domain))
                        {
                            clusterMonitor.RegisterSelf(new[] {messageIdentifier}, message.Domain);
                        }
                        else
                        {
                            logger.Warn($"MessageIdentity {messageIdentifier.Identity.GetString()} doesn't belong to requested " +
                                        $"Domain {message.Domain}!");
                        }
                    }
                }
            }

            return shouldHandle;
        }

        private bool IsDiscoverMessageRouteRequest(IMessage message)
            => message.Equals(KinoMessages.DiscoverMessageRoute);
    }
}