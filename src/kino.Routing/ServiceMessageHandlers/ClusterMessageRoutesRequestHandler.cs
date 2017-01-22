using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class ClusterMessageRoutesRequestHandler : IServiceMessageHandler
    {
        private readonly ISecurityProvider securityProvider;
        private readonly INodeRoutesRegistrar nodeRoutesRegistrar;

        public ClusterMessageRoutesRequestHandler(ISecurityProvider securityProvider,
                                                  INodeRoutesRegistrar nodeRoutesRegistrar)
        {
            this.securityProvider = securityProvider;
            this.nodeRoutesRegistrar = nodeRoutesRegistrar;
        }

        public void Handle(IMessage message, ISocket _)
        {
            if (securityProvider.DomainIsAllowed(message.Domain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                nodeRoutesRegistrar.RegisterOwnGlobalRoutes(message.Domain);
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.RequestClusterMessageRoutes;
    }
}