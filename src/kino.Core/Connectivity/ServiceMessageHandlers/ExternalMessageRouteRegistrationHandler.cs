using System;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity.ServiceMessageHandlers
{
    public class ExternalMessageRouteRegistrationHandler : IServiceMessageHandler
    {
        private readonly IExternalRoutingTable externalRoutingTable;
        private readonly ILogger logger;
        private static readonly MessageIdentifier RegisterExternalMessageRouteMessageIdentifier = MessageIdentifier.Create<RegisterExternalMessageRouteMessage>();
        private readonly IClusterMembership clusterMembership;

        public ExternalMessageRouteRegistrationHandler(IExternalRoutingTable externalRoutingTable, IClusterMembership clusterMembership, ILogger logger)
        {
            this.externalRoutingTable = externalRoutingTable;
            this.logger = logger;
            this.clusterMembership = clusterMembership;
        }

        public bool Handle(IMessage message, ISocket forwardingSocket)
        {
            var shouldHandle = IsExternalRouteRegistration(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();
                clusterMembership.AddClusterMember(new SocketEndpoint(new Uri(payload.Uri), payload.SocketIdentity));

                var handlerSocketIdentifier = new SocketIdentifier(payload.SocketIdentity);
                var uri = new Uri(payload.Uri);

                foreach (var registration in payload.MessageContracts)
                {
                    try
                    {
                        var messageHandlerIdentifier = new MessageIdentifier(registration.Version, registration.Identity);
                        externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, handlerSocketIdentifier, uri);
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }

            return shouldHandle;
        }

        private static bool IsExternalRouteRegistration(IMessage message)
            => message.Equals(RegisterExternalMessageRouteMessageIdentifier);
    }
}