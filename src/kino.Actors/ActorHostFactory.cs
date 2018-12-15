using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Security;

namespace kino.Actors
{
    public class ActorHostFactory : IActorHostFactory
    {
        private readonly ISecurityProvider securityProvider;
        private readonly ILocalSocketFactory localSocketFactory;
        private readonly ILogger logger;

        public ActorHostFactory(ISecurityProvider securityProvider,
                                ILocalSocketFactory localSocketFactory,
                                ILogger logger)
        {
            this.securityProvider = securityProvider;
            this.localSocketFactory = localSocketFactory;
            this.logger = logger;
        }

        public IActorHost Create()
            => new ActorHost(new ActorHandlerMap(),
                             new AsyncQueue<AsyncMessageContext>(),
                             new AsyncQueue<ActorRegistration>(),
                             securityProvider,
                             localSocketFactory,
                             logger);
    }
}