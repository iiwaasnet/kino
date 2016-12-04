using System;
using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using Health = kino.Cluster.Health;

namespace kino.Routing.ServiceMessageHandlers
{
    public class ExternalMessageRouteRegistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ILogger logger;
        private readonly ISecurityProvider securityProvider;
        private readonly IClusterServices clusterServices;

        public ExternalMessageRouteRegistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       IClusterServices clusterServices,
                                                       ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.securityProvider = securityProvider;
            this.clusterServices = clusterServices;
        }

        public bool Handle(IMessage message, ISocket _)
        {
            var shouldHandle = IsExternalRouteRegistration(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();
                    var peer = new Node(new Uri(payload.Uri), payload.NodeIdentity);
                    var health = new Health
                                 {
                                     Uri = payload.Health.Uri,
                                     HeartBeatInterval = payload.Health.HeartBeatInterval
                                 };

                    foreach (var route in payload.Routes)
                    {
                        var receiver = new ReceiverIdentifier(route.ReceiverIdentity);
                        var messageRoutes = receiver.IsMessageHub()
                                                ? new MessageRoute {Receiver = receiver}.ToEnumerable()
                                                : route.MessageContracts.Select(mc => new MessageRoute
                                                                                      {
                                                                                          Receiver = receiver,
                                                                                          Message = new MessageIdentifier(mc.Identity, mc.Version, mc.Partition)
                                                                                      });
                        foreach (var messageRoute in messageRoutes)
                        {
                            try
                            {
                                //TODO: Refactor, hence messageIdentifier.IsMessageHub() should be first condition
                                if (receiver.IsMessageHub() || securityProvider.GetDomain(messageRoute.Message.Identity) == message.Domain)
                                {
                                    clusterServices.AddPeer(new Node(payload.Uri, payload.NodeIdentity), health);

                                    externalRoutingTable.AddMessageRoute(new ExternalRouteRegistration
                                                                         {
                                                                             Route = messageRoute,
                                                                             Peer = peer,
                                                                             Health = health
                                                                         });
                                }
                                else
                                {
                                    logger.Warn($"MessageIdentity {messageRoute.Message} doesn't belong to requested Domain {message.Domain}!");
                                }
                            }
                            catch (Exception err)
                            {
                                logger.Error(err);
                            }
                        }
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsExternalRouteRegistration(IMessage message)
            => message.Equals(KinoMessages.RegisterExternalMessageRoute);
    }
}