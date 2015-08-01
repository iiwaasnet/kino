using System.Collections.Generic;
using rawf.Connectivity;

namespace rawf.Actors
{
    public interface IActorHandlerMap
    {
        void Add(IActor actor);
        MessageHandler Get(MessageHandlerIdentifier identifier);
        IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers();
    }
}