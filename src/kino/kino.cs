using System;
using kino.Actors;
using kino.Client;
using kino.Core.Framework;
using kino.Routing;

namespace kino
{
    public partial class kino : IDisposable
    {
        private Func<IMessageHub> getMessageHub;
        private Func<bool, IMessageHub> createMessageHub;
        private IActorHostManager actorHostManager;
        private IMessageRouter messageRouter;
        private ActorHostManager internalActorHostManager;
        private IDependencyResolver resolver;
        private bool isBuilt;
        private readonly TimeSpan startupDelay = TimeSpan.FromMilliseconds(300);

        public kino()
        {
        }

        public kino(IDependencyResolver resolver)
        {
            this.resolver = resolver;
            Build();
        }

        public void SetResolver(IDependencyResolver resolver)
            => this.resolver = resolver;

        public IMessageHub GetMessageHub()
        {
            AssertKinoBuilt();
            return getMessageHub();
        }

        public IMessageHub CreateMessageHub(bool keepRegistrationLocal)
        {
            AssertKinoBuilt();
            return createMessageHub(keepRegistrationLocal);
        }

        public void AssignActor(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy = ActorHostInstancePolicy.TryReuseExisting)
        {
            AssertKinoBuilt();
            actorHostManager.AssignActor(actor, actorHostInstancePolicy);
        }

        public void Start()
        {
            AssertKinoBuilt();
            messageRouter.Start();
            startupDelay.Sleep();
        }

        public void Stop()
        {
            messageRouter.Stop();
        }

        public void Dispose()
        {
            actorHostManager.Dispose();
            internalActorHostManager.Dispose();
        }

        private void AssertDependencyResolverSet()
        {
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver), "Dependency resolver is not assigned!");
            }
        }

        private void AssertKinoBuilt()
        {
            if (!isBuilt)
            {
                throw new InvalidOperationException($"Call kino.{nameof(Build)} first to build all dependencies!");
            }
        }
    }
}