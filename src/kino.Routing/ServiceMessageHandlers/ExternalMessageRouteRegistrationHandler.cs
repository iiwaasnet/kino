using System;
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
        private readonly IClusterConnectivity clusterConnectivity;

        public ExternalMessageRouteRegistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       IClusterConnectivity clusterConnectivity,
                                                       ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.securityProvider = securityProvider;
            this.clusterConnectivity = clusterConnectivity;
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
                    var uri = new Uri(payload.Uri);
                    var memberAdded = false;

                    foreach (var registration in payload.MessageContracts)
                    {
                        try
                        {
                            var messageIdentifier = registration.ToIdentifier();
                            //TODO: Refactor, hence messageIdentifier.IsMessageHub() should be first condition
                            if (messageIdentifier.IsMessageHub() || securityProvider.GetDomain(messageIdentifier.Identity) == message.Domain)
                            {
                                var health = new Health
                                             {
                                                 Uri = payload.Health.Uri,
                                                 HeartBeatInterval = payload.Health.HeartBeatInterval
                                             };
                                clusterConnectivity.AddPeer(new Node(payload.Uri, payload.SocketIdentity), health);
                                externalRoutingTable.AddMessageRoute(new ExternalRouteDefinition
                                                                     {
                                                                         Identifier = messageIdentifier,
                                                                         Peer = new Node(uri, payload.SocketIdentity),
                                                                         Health = health
                                                                     });
                            }
                            else
                            {
                                logger.Warn($"MessageIdentity {messageIdentifier.Identity.GetString()} doesn't belong to requested " +
                                            $"Domain {message.Domain}!");
                            }
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
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