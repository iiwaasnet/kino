using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
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

        public IEnumerable<MessageIdentifier> Add(IActor actor)
        {
            var tmp = new List<MessageIdentifier>();
            foreach (var reg in GetActorRegistrations(actor))
            {
                if (messageHandlers.TryAdd(reg.Key, reg.Value))
                {
                    tmp.Add(reg.Key);
                }
                else
                {
                    CleanupIncompleteRegistration(tmp);

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

        private static IEnumerable<KeyValuePair<MessageIdentifier, MessageHandler>> GetActorRegistrations(IActor actor)
            => actor
                .GetInterfaceDefinition()
                .Select(messageMap =>
                        new KeyValuePair<MessageIdentifier, MessageHandler>(new MessageIdentifier(messageMap.Message.Version,
                                                                                                  messageMap.Message.Identity),
                                                                            messageMap.Handler));
    }
}