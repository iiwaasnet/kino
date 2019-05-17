using kino.Connectivity.Kafka;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class NodeMessageRoutesRequestHandler : IKafkaServiceMessageHandler
    {
        private readonly ISecurityProvider securityProvider;
        private readonly INodeRoutesRegistrar nodeRoutesRegistrar;

        public NodeMessageRoutesRequestHandler(ISecurityProvider securityProvider,
                                               INodeRoutesRegistrar nodeRoutesRegistrar)
        {
            this.securityProvider = securityProvider;
            this.nodeRoutesRegistrar = nodeRoutesRegistrar;
        }

        public void Handle(IMessage message, ISender _)
        {
            if (securityProvider.DomainIsAllowed(message.Domain))
            {
                message.As<Message>().VerifySignature(securityProvider);

                nodeRoutesRegistrar.RegisterOwnGlobalRoutes(message.Domain);
            }
        }

        public MessageIdentifier TargetMessage => KinoMessages.RequestNodeMessageRoutes;
    }
}