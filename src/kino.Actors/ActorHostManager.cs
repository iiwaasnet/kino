using System;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using Microsoft.Extensions.Logging;

namespace kino.Actors
{
    public class ActorHostManager : IActorHostManager
    {
        private readonly IActorHostFactory actorHostFactory;
        private readonly ILogger logger;
        private readonly IList<IActorHost> actorHosts;
        private readonly object @lock = new object();
        private bool isDisposed = false;

        public ActorHostManager(IActorHostFactory actorHostFactory,
                                ILogger logger)
        {
            this.actorHostFactory = actorHostFactory;
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
                actorHost = actorHostFactory.Create();
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
                logger.LogError(err);
            }
        }
    }
}