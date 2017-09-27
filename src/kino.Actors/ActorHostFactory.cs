using kino.Connectivity;
using kino.Core.Framework;
using kino.Messaging;
using kino.Routing;
using kino.Security;
using Microsoft.Extensions.Logging;

namespace kino.Actors
{
    public class ActorHostFactory : IActorHostFactory
    {
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender;
        private readonly ILocalSocketFactory localSocketFactory;
        private readonly ILogger logger;

        public ActorHostFactory(ISecurityProvider securityProvider,
                                ILocalSocket<IMessage> localRouterSocket,
                                ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender,
                                ILocalSocketFactory localSocketFactory,
                                ILogger logger)
        {
            this.securityProvider = securityProvider;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsSender = internalRegistrationsSender;
            this.localSocketFactory = localSocketFactory;
            this.logger = logger;
        }

        public IActorHost Create()
            => new ActorHost(new ActorHandlerMap(),
                             new AsyncQueue<AsyncMessageContext>(),
                             new AsyncQueue<ActorRegistration>(),
                             securityProvider,
                             localRouterSocket,
                             internalRegistrationsSender,
                             localSocketFactory,
                             logger);
    }
}