using System.Collections.Generic;
using kino.Core;

namespace kino.Actors
{
    public interface IActorHandlerMap
    {
        IEnumerable<ActorMessageHandlerIdentifier> Add(IActor actor);

        bool CanAdd(IActor actor);

        MessageHandler Get(MessageIdentifier identifier);
    }
}