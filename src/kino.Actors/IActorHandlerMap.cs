using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Actors
{
    public interface IActorHandlerMap
    {
        IEnumerable<MessageIdentifier> Add(IActor actor);
        MessageHandler Get(MessageIdentifier identifier);
        IEnumerable<MessageIdentifier> GetMessageHandlerIdentifiers();
    }
}