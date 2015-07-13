using System.Collections.Generic;

namespace rawf.Actors
{
    public interface IActorHandlersMap
    {
        void Add(IActor actor);
        MessageHandler Get(ActorIdentifier identifier);
        IEnumerable<ActorIdentifier> GetRegisteredIdentifiers();
    }
}