using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using rawf.Framework;

namespace rawf.Actors
{
    public class ActorHandlersMap : IActorHandlersMap
    {
        private readonly ConcurrentDictionary<ActorIdentifier, MessageHandler> messageHandlers;

        public ActorHandlersMap()
        {
            messageHandlers = new ConcurrentDictionary<ActorIdentifier, MessageHandler>();
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

        public MessageHandler Get(ActorIdentifier identifier)
        {
            MessageHandler value;
            if (messageHandlers.TryGetValue(identifier, out value))
            {
                return value;
            }

            throw new KeyNotFoundException(identifier.ToString());
        }

        public IEnumerable<ActorIdentifier> GetRegisteredIdentifiers()
        {
            return messageHandlers.Keys;
        }

        private static IEnumerable<KeyValuePair<ActorIdentifier, MessageHandler>> GetActorRegistrations(IActor actor)
        {
            return actor
                .GetInterfaceDefinition()
                .Select(messageMap => new KeyValuePair<ActorIdentifier, MessageHandler>(new ActorIdentifier(messageMap.Message.Version,
                                                                                                            messageMap.Message.Identity),
                                                                                        messageMap.Handler));
        }
    }
}