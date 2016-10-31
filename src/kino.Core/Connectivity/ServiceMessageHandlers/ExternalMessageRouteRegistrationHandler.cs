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
        private readonly ISecurityProvider securityProvider;

        public ExternalMessageRouteRegistrationHandler(IExternalRoutingTable externalRoutingTable,
                                                       IClusterMembership clusterMembership,
                                                       ISecurityProvider securityProvider,
                                                       ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.clusterMembership = clusterMembership;
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
                                if (!memberAdded)
                                {
                                    clusterMembership.AddClusterMember(new SocketEndpoint(uri, payload.SocketIdentity));
                                    memberAdded = true;
                                }
                                var peerConnection = externalRoutingTable.AddMessageRoute(messageIdentifier, handlerSocketIdentifier, uri);
                                //TODO: Remove this code from here, no connection to be established!!!
                                if (!peerConnection.Connected)
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