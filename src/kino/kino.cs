using System;
using kino.Actors;
using kino.Client;

namespace kino
{
    public partial class kino
    {
        private readonly IDependencyResolver resolver;
        private bool isStarted;

        public kino(IDependencyResolver resolver)
        {
            this.resolver = resolver;
        }

        public IMessageHub GetMessageHub()
        {
            AssertKinoStarted();
            return getMessageHub();
        }

        public IMessageHub CreateMessageHub(bool keepRegistrationLocal)
        {
            AssertKinoStarted();
            return createMessageHub(keepRegistrationLocal);
        }

        public void AssignActor(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy = ActorHostInstancePolicy.TryReuseExisting)
        {
            AssertKinoStarted();
            actorHostManager.AssignActor(actor, actorHostInstancePolicy);
        }

        public void Start()
        {
            messageRouter.Start();
            isStarted = true;
        }

        public void Stop()
        {
            messageRouter.Stop();
            isStarted = false;
        }

        private void AssertKinoStarted()
        {
            if (!isStarted)
            {
                throw new InvalidOperationException("Kino is not started yet! Call kino.Start() must happen first.");
            }
        }
    }
}