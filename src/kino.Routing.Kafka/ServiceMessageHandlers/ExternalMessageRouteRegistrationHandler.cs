using System;
using System.Linq;
using kino.Cluster;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using Health = kino.Cluster.Health;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class ExternalMessageRouteRegistrationHandler : IKafkaServiceMessageHandler
    {
        private readonly IKafkaExternalRoutingTable externalRoutingTable;
        private readonly ILogger logger;
        private readonly ISecurityProvider securityProvider;
        private readonly IClusterHealthMonitor clusterHealthMonitor;

        public ExternalMessageRouteRegistrationHandler(IKafkaExternalRoutingTable externalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       IClusterHealthMonitor clusterHealthMonitor,
                                                       ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.securityProvider = securityProvider;
            this.clusterHealthMonitor = clusterHealthMonitor;
        }

        public void Handle(IMessage message, ISender _)
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
                var peerAddedForMonitoring = false;
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
                            //NOTE: Keep the order in if(...), hence MessageHub is not registered to receive any specific message
                            if (receiver.IsMessageHub() || securityProvider.GetDomain(messageRoute.Message.Identity) == message.Domain)
                            {
                                if (!peerAddedForMonitoring)
                                {
                                    clusterHealthMonitor.AddPeer(peer, health);
                                    peerAddedForMonitoring = true;
                                }

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

        public MessageIdentifier TargetMessage => KinoMessages.RegisterExternalMessageRoute;
    }
}