using System.Collections.Generic;
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
        private readonly IClusterMonitor clusterMonitor;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public MessageRouteDiscoveryHandler(IClusterMonitor clusterMonitor,
                                            IInternalRoutingTable internalRoutingTable,
                                            ISecurityProvider securityProvider,
                                            ILogger logger)
        {
            this.clusterMonitor = clusterMonitor;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public void Handle(IMessage message, ISocket _)
        {
            if (securityProvider.DomainIsAllowed(message.Domain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                var payload = message.GetPayload<DiscoverMessageRouteMessage>();
                var internalRoutes = internalRoutingTable.GetAllRoutes();
                var messageRoutes = new List<Cluster.MessageRoute>();
                if (payload.ReceiverIdentity.IsMessageHub())
                {
                    messageRoutes.AddRange(internalRoutes.MessageHubs
                                                         .Where(mh => !mh.LocalRegistration
                                                                      && mh.MessageHub.Equals(new ReceiverIdentifier(payload.ReceiverIdentity)))
                                                         .Select(mh => new Cluster.MessageRoute {Receiver = mh.MessageHub}));
                }
                else
                {
                    var messageContract = new MessageIdentifier(payload.MessageContract.Identity, payload.MessageContract.Version, payload.MessageContract.Partition);
                    messageRoutes.AddRange(internalRoutes.Actors
                                                         .Where(r => r.Message.Equals(messageContract))
                                                         .SelectMany(r => r.Actors
                                                                           .Where(a => !a.LocalRegistration)
                                                                           .Select(a => new Cluster.MessageRoute
                                                                                        {
                                                                                            Receiver = a,
                                                                                            Message = messageContract
                                                                                        })));
                }
                foreach (var messageRoute in messageRoutes)
                {
                    var domains = messageRoute.Receiver.IsMessageHub()
                                      ? securityProvider.GetAllowedDomains()
                                      : new[] {securityProvider.GetDomain(messageRoute.Message.Identity)};
                    if (domains.Contains(message.Domain))
                    {
                        clusterMonitor.RegisterSelf(messageRoute.ToEnumerable(), message.Domain);
                    }
                    else
                    {
                        logger.Warn($"MessageIdentity {messageRoute.Message} doesn't belong to requested Domain {message.Domain}!");
                    }
                }
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.DiscoverMessageRoute;
    }
}