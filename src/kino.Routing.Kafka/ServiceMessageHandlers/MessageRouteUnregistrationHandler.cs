﻿using System.Collections.Generic;
using System.Linq;
using kino.Cluster;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class MessageRouteUnregistrationHandler : IKafkaServiceMessageHandler
    {
        private readonly IClusterHealthMonitor clusterHealthMonitor;
        private readonly IKafkaExternalRoutingTable externalRoutingTable;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;

        public MessageRouteUnregistrationHandler(IClusterHealthMonitor clusterHealthMonitor,
                                                 IKafkaExternalRoutingTable externalRoutingTable,
                                                 ISecurityProvider securityProvider,
                                                 ILogger logger)
        {
            this.clusterHealthMonitor = clusterHealthMonitor;
            this.externalRoutingTable = externalRoutingTable;
            this.securityProvider = securityProvider;
            this.logger = logger;
        }

        public void Handle(IMessage message, ISender scaleOutBackend)
        {
            if (securityProvider.DomainIsAllowed(message.Domain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                var nodeIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
                foreach (var route in GetUnregistrationRoutes(payload, message.Domain))
                {
                    var peerRemoveResult = externalRoutingTable.RemoveMessageRoute(route);
                    if (peerRemoveResult.ConnectionAction == BrokerConnectionAction.Disconnect)
                    {
                        scaleOutBackend.Disconnect(peerRemoveResult.AppCluster.BrokerName);
                    }

                    if (peerRemoveResult.ConnectionAction != BrokerConnectionAction.KeepConnection)
                    {
                        clusterHealthMonitor.DeletePeer(nodeIdentifier);
                    }
                }
            }
        }

        private IEnumerable<ExternalRouteRemoval> GetUnregistrationRoutes(UnregisterMessageRouteMessage payload, string domain)
        {
            foreach (var route in payload.Routes.SelectMany(r => r.MessageContracts.Select(mc => new MessageRoute
                                                                                                 {
                                                                                                     Receiver = new ReceiverIdentifier(r.ReceiverIdentity),
                                                                                                     Message = new MessageIdentifier(mc.Identity, mc.Version, mc.Partition)
                                                                                                 })))
            {
                if (route.Receiver.IsMessageHub() || securityProvider.GetDomain(route.Message.Identity) == domain)
                {
                    yield return new ExternalRouteRemoval
                                 {
                                     Route = route,
                                     NodeIdentifier = payload.ReceiverNodeIdentity
                                 };
                }
                else
                {
                    logger.Warn($"MessageIdentity {route.Message} doesn't belong to requested Domain {domain}!");
                }
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.UnregisterMessageRoute;
    }
}