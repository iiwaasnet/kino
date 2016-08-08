using System;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class ExternalMessageRouteRegistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ILogger logger;
        private readonly IClusterMembership clusterMembership;
        private readonly RouterConfiguration config;
        private readonly ISecurityProvider securityProvider;

        public ExternalMessageRouteRegistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                       IClusterMembership clusterMembership,
                                                       RouterConfiguration config,
                                                       ISecurityProvider securityProvider,
                                                       ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.clusterMembership = clusterMembership;
            this.config = config;
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
                    clusterMembership.AddClusterMember(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));

                    var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                    var uri = new Uri(payload.Uri);
                    foreach (var registration in payload.MessageContracts)
                    {
                        try
                        {
                            var messageIdentifier = new MessageIdentifier(registration.Version,
                                                                          registration.Identity,
                                                                          registration.Partition);
                            //TODO: Refactor, hence messageIdentifier.IsMessageHub() should be first condition
                            if (messageIdentifier.IsMessageHub() || securityProvider.GetDomain(messageIdentifier.Identity) == message.Domain)
                            {
                                var peerConnection = externalRoutingTable.AddMessageRoute(messageIdentifier, handlerSocketIdentifier, uri);
                                if (!config.DeferPeerConnection && !peerConnection.Connected)
                                {
                                    forwardingSocket.Connect(uri);
                                    peerConnection.Connected = true;
                                }
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