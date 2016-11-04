using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class MessageRouteDiscoveryHandler : IServiceMessageHandler
    {
        private readonly IClusterConnectivity clusterConnectivity;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public MessageRouteDiscoveryHandler(IClusterConnectivity clusterConnectivity,
                                            IInternalRoutingTable internalRoutingTable,
                                            ISecurityProvider securityProvider,
                                            ILogger logger)
        {
            this.clusterConnectivity = clusterConnectivity;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public bool Handle(IMessage message, ISocket _)
        {
            var shouldHandle = IsDiscoverMessageRouteRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var messageContract = message.GetPayload<DiscoverMessageRouteMessage>().MessageContract;
                    var messageIdentifier = messageContract.ToIdentifier();
                    if (internalRoutingTable.MessageHandlerRegisteredExternaly(messageIdentifier))
                    {
                        var domains = messageIdentifier.IsMessageHub()
                                          ? securityProvider.GetAllowedDomains()
                                          : new[] {securityProvider.GetDomain(messageIdentifier.Identity)};
                        if (domains.Contains(message.Domain))
                        {
                            clusterConnectivity.RegisterSelf(new[] {messageIdentifier}, message.Domain);
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