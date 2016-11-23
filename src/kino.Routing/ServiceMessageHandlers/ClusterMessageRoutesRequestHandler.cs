using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using MessageRoute = kino.Cluster.MessageRoute;

namespace kino.Routing.ServiceMessageHandlers
{
    public class ClusterMessageRoutesRequestHandler : IServiceMessageHandler
    {
        private readonly IClusterServices clusterServices;
        private readonly IInternalRoutingTable internalRoutingTable;
        private readonly ISecurityProvider securityProvider;

        public ClusterMessageRoutesRequestHandler(IClusterServices clusterServices,
                                                  IInternalRoutingTable internalRoutingTable,
                                                  ISecurityProvider securityProvider)
        {
            this.clusterServices = clusterServices;
            this.internalRoutingTable = internalRoutingTable;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket _)
        {
            var shouldHandle = IsRoutesRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var routes = internalRoutingTable.GetAllRoutes();
                    var messageHubs = routes.MessageHubs;
                    var actors = routes.Actors;
                    var contracts = actors.SelectMany(r => r.Actors.Select(a => new MessageRoute
                                                                                {
                                                                                    Receiver = new ReceiverIdentifier(a.Identity),
                                                                                    Message = r.Message
                                                                                }))
                                          .Where(r => securityProvider.GetDomain(r.Message.Identity) == message.Domain)
                                          .Concat(messageHubs.Select(mh => new MessageRoute {Receiver = new ReceiverIdentifier(mh.MessageHub.Identity)}))
                                          .ToList();

                    if (contracts.Any())
                    {
                        clusterServices.RegisterSelf(contracts, message.Domain);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsRoutesRequest(IMessage message)
            => message.Equals(KinoMessages.RequestClusterMessageRoutes);
    }
}