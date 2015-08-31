using System.Collections.Generic;
using kino.Connectivity;

namespace kino.Actors
{
    public interface IActorHandlerMap
    {
        IEnumerable<MessageHandlerIdentifier> Add(IActor actor);
        MessageHandler Get(MessageHandlerIdentifier identifier);
        IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers();
    }
}