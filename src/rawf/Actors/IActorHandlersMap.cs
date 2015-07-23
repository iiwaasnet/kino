using System.Collections.Generic;

namespace rawf.Actors
{
    public interface IActorHandlersMap
    {
        void Add(IActor actor);
        MessageHandler Get(MessageHandlerIdentifier identifier);
        IEnumerable<MessageHandlerIdentifier> GetRegisteredIdentifiers();
    }
}