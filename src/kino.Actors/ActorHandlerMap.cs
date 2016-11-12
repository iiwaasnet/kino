using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;

namespace kino.Actors
{
    public class ActorHandlerMap : IActorHandlerMap
    {
        private readonly ConcurrentDictionary<MessageIdentifier, MessageHandler> messageHandlers;

        public ActorHandlerMap()
        {
            messageHandlers = new ConcurrentDictionary<MessageIdentifier, MessageHandler>();
        }

        public IEnumerable<ActorMessageHandlerIdentifier> Add(IActor actor)
        {
            var tmp = new List<ActorMessageHandlerIdentifier>();
            foreach (var reg in GetActorRegistrations(actor))
            {
                if (messageHandlers.TryAdd(reg.Key, reg.Value.Handler))
                {
                    tmp.Add(new ActorMessageHandlerIdentifier {Identifier = reg.Key, KeepRegistrationLocal = reg.Value.KeepRegistrationLocal});
                }
                else
                {
                    CleanupIncompleteRegistration(tmp.Select(t => t.Identifier));

                    throw new DuplicatedKeyException(reg.Key.ToString());
                }
            }

            return tmp;
        }

        private void CleanupIncompleteRegistration(IEnumerable<MessageIdentifier> incomplete)
        {
            foreach (var identifier in incomplete)
            {
                MessageHandler _;
                messageHandlers.TryRemove(identifier, out _);
            }
        }

        public bool CanAdd(IActor actor)
            => GetActorRegistrations(actor).All(reg => !messageHandlers.ContainsKey(reg.Key));

        public MessageHandler Get(MessageIdentifier identifier)
        {
            MessageHandler value;
            if (messageHandlers.TryGetValue(identifier, out value))
            {
                return value;
            }

            throw new KeyNotFoundException(identifier.ToString());
        }

        internal IEnumerable<MessageIdentifier> GetMessageHandlerIdentifiers()
            => messageHandlers.Keys;

        private static IEnumerable<KeyValuePair<MessageIdentifier, MessageHandlerDefinition>> GetActorRegistrations(IActor actor)
            => actor
                .GetInterfaceDefinition()
                .Select(messageMap =>
                            new KeyValuePair<MessageIdentifier, MessageHandlerDefinition>(new MessageIdentifier(messageMap.Message.Identity,
                                                                                                                messageMap.Message.Version,
                                                                                                                messageMap.Message.Partition),
                                                                                          messageMap));
    }
}