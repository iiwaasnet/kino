using System.Collections.Generic;
using kino.Connectivity;
using kino.Messaging;

namespace kino.Actors
{
    public interface IActorHandlerMap
    {
        IEnumerable<IMessageIdentifier> Add(IActor actor);
        MessageHandler Get(MessageIdentifier identifier);
        IEnumerable<IMessageIdentifier> GetMessageHandlerIdentifiers();
    }
}