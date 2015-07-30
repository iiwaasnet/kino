using System.Collections.Generic;

namespace rawf.Backend
{
    public interface IActorHandlersMap
    {
        void Add(IActor actor);
        MessageHandler Get(MessageHandlerIdentifier identifier);
        IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers();
    }
}