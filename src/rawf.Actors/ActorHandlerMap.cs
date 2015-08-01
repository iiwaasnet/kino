using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using rawf.Connectivity;
using rawf.Framework;

namespace rawf.Actors
{
    public class ActorHandlerMap : IActorHandlerMap
    {
        private readonly ConcurrentDictionary<MessageHandlerIdentifier, MessageHandler> messageHandlers;

        public ActorHandlerMap()
        {
            messageHandlers = new ConcurrentDictionary<MessageHandlerIdentifier, MessageHandler>();
        }

        public void Add(IActor actor)
        {
            foreach (var reg in GetActorRegistrations(actor))
            {
                if (!messageHandlers.TryAdd(reg.Key, reg.Value))
                {
                    throw new DuplicatedKeyException(reg.Key.ToString());
                }
            }
        }

        public MessageHandler Get(MessageHandlerIdentifier identifier)
        {
            MessageHandler value;
            if (messageHandlers.TryGetValue(identifier, out value))
            {
                return value;
            }

            throw new KeyNotFoundException(identifier.ToString());
        }

        public IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers()
        {
            return messageHandlers.Keys;
        }

        private static IEnumerable<KeyValuePair<MessageHandlerIdentifier, MessageHandler>> GetActorRegistrations(IActor actor)
        {
            return actor
                .GetInterfaceDefinition()
                .Select(messageMap =>
                        new KeyValuePair<MessageHandlerIdentifier, MessageHandler>(
                            new MessageHandlerIdentifier(messageMap.Message.Version,
                                                         messageMap.Message.Identity),
                            messageMap.Handler));
        }
    }
}