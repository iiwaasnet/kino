using System;

namespace kino.Actors
{
    public interface IActorHostManager : IDisposable
    {
        void AssignActor(IActor actor, ActorHostInstancePolicy actorHostInstancePolicy = ActorHostInstancePolicy.TryReuseExisting);
    }
}