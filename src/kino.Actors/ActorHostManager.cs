using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Routing;
using kino.Security;

namespace kino.Actors
{
    public class ActorHostManager : IActorHostManager
    {
        private readonly ISecurityProvider securityProvider;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ILocalSocket<IMessage> localRouterSocket;
        private readonly ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender;
        private readonly ILocalSocketFactory localSocketFactory;
        private readonly ILogger logger;
        private readonly IList<IActorHost> actorHosts;
        private readonly object @lock = new object();
        private bool isDisposed = false;

        public ActorHostManager(ISecurityProvider securityProvider,
                                IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                ILocalSocket<IMessage> localRouterSocket,
                                ILocalSendingSocket<InternalRouteRegistration> internalRegistrationsSender,
                                ILocalSocketFactory localSocketFactory,
                                ILogger logger)
        {
            this.securityProvider = securityProvider;
            this.performanceCounterManager = performanceCounterManager;
            this.localRouterSocket = localRouterSocket;
            this.internalRegistrationsSender = internalRegistrationsSender;
            this.localSocketFactory = localSocketFactory;
            this.logger = logger;
            actorHosts = new List<IActorHost>();
        }

        public void AssignActor(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy = ActorHostInstancePolicy.TryReuseExisting)
        {
            AssertNotDisposed();

            lock (@lock)
            {
                GetOrCreateActorHost(actor, actorHostInstancePolicy).AssignActor(actor);
            }
        }

        private void AssertNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private IActorHost GetOrCreateActorHost(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy)
        {
            var actorHost = actorHosts.FirstOrDefault(ah => ah.CanAssignActor(actor));

            if (actorHostInstancePolicy == ActorHostInstancePolicy.AlwaysCreateNew
                || !actorHosts.Any()
                || actorHost == null)
            {
                actorHost = new ActorHost(new ActorHandlerMap(),
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          securityProvider,
                                          performanceCounterManager,
                                          localRouterSocket,
                                          internalRegistrationsSender,
                                          localSocketFactory,
                                          logger);
                actorHost.Start();
                actorHosts.Add(actorHost);
            }

            return actorHost;
        }

        public void Dispose()
        {
            try
            {
                actorHosts.ForEach(actorHost => actorHost.Stop());
                actorHosts.Clear();

                isDisposed = true;
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }
    }
}