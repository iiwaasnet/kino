using kino.Connectivity;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Routing.ServiceMessageHandlers
{
    public class NodeMessageRoutesRequestHandler : IServiceMessageHandler
    {
        private readonly ISecurityProvider securityProvider;
        private readonly INodeRoutesRegistrar nodeRoutesRegistrar;

        public NodeMessageRoutesRequestHandler(ISecurityProvider securityProvider,
                                               INodeRoutesRegistrar nodeRoutesRegistrar)
        {
            this.securityProvider = securityProvider;
            this.nodeRoutesRegistrar = nodeRoutesRegistrar;
        }

        public bool Handle(IMessage message, ISocket _)
        {
            var shouldHandle = IsMessageRoutesRequest(message);
            if (shouldHandle)
            {
                if (securityProvider.DomainIsAllowed(message.Domain))
                {
                    message.As<Message>().VerifySignature(securityProvider);

                    nodeRoutesRegistrar.RegisterOwnGlobalRoutes(message.Domain);
                }
            }

            return shouldHandle;
        }

        private static bool IsMessageRoutesRequest(IMessage message)
            => message.Equals(KinoMessages.RequestNodeMessageRoutes);
    }
}