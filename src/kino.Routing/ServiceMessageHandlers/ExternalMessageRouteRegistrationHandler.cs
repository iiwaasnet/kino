using System;
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

        public ExternalMessageRouteRegistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                       ISecurityProvider securityProvider,
                                                       ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.securityProvider = securityProvider;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
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
                    var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);

                    foreach (var registration in payload.MessageContracts)
                    {
                        try
                        {
                            var messageIdentifier = registration.IsAnyIdentifier
                                                        ? (Identifier) new AnyIdentifier(registration.Identity)
                                                        : (Identifier) new MessageIdentifier(registration.Identity,
                                                                                             registration.Version,
                                                                                             registration.Partition);
                            //TODO: Refactor, hence messageIdentifier.IsMessageHub() should be first condition
                            if (messageIdentifier.IsMessageHub() || securityProvider.GetDomain(messageIdentifier.Identity) == message.Domain)
                            {
                                var peerConnection = externalRoutingTable.AddMessageRoute(new ExternalRouteDefinition
                                                                                          {
                                                                                              Identifier = messageIdentifier,
                                                                                              Peer = new Node(uri, payload.SocketIdentity),
                                                                                              Health = new Health
                                                                                                       {
                                                                                                           Uri = payload.Health.Uri,
                                                                                                           HeartBeatInterval = payload.Health.HeartBeatInterval
                                                                                                       }
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