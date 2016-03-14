using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Sockets;

namespace kino.Actors
{
    public class ActorHostManager : IActorHostManager
    {
        private readonly ISocketFactory socketFactory;
        private readonly RouterConfiguration routerConfiguration;
        private readonly ILogger logger;
        private readonly IList<IActorHost> actorHosts;
        private readonly object @lock = new object();
        private bool isDisposed = false;

        public ActorHostManager(ISocketFactory socketFactory,
                                RouterConfiguration routerConfiguration,
                                ILogger logger)
        {
            this.socketFactory = socketFactory;
            this.routerConfiguration = routerConfiguration;
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
                actorHost = new ActorHost(socketFactory,
                                          new ActorHandlerMap(),
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(), 
                                          routerConfiguration,
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