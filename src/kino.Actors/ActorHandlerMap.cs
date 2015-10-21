using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kino.Connectivity;
using kino.Framework;
using kino.Messaging;

namespace kino.Actors
{
    public class ActorHandlerMap : IActorHandlerMap
    {
        private readonly ConcurrentDictionary<IMessageIdentifier, MessageHandler> messageHandlers;

        public ActorHandlerMap()
        {
            messageHandlers = new ConcurrentDictionary<IMessageIdentifier, MessageHandler>();
        }

        public IEnumerable<IMessageIdentifier> Add(IActor actor)
        {
            var tmp = new List<IMessageIdentifier>();
            foreach (var reg in GetActorRegistrations(actor))
            {
                if (messageHandlers.TryAdd(reg.Key, reg.Value))
                {
                    tmp.Add(reg.Key);
                }
                else
                {
                    throw new DuplicatedKeyException(reg.Key.ToString());
                }
            }

            if (!tmp.Any())
            {
                throw new Exception($"Actor {actor.GetType().FullName} seems to not handle any message!");
            }

            return tmp;
        }

        public MessageHandler Get(MessageIdentifier identifier)
        {
            MessageHandler value;
            if (messageHandlers.TryGetValue(identifier, out value))
            {
                return value;
            }

            throw new KeyNotFoundException(identifier.ToString());
        }

        public IEnumerable<IMessageIdentifier> GetMessageHandlerIdentifiers()
        {
            return messageHandlers.Keys;
        }

        private static IEnumerable<KeyValuePair<IMessageIdentifier, MessageHandler>> GetActorRegistrations(IActor actor)
            => actor
                .GetInterfaceDefinition()
                .Select(messageMap =>
                        new KeyValuePair<IMessageIdentifier, MessageHandler>(new MessageIdentifier(messageMap.Message.Version,
                                                                                                   messageMap.Message.Identity),
                                                                             messageMap.Handler));

        private static bool InterfaceMethodFilter(MemberInfo memberInfo, object filterCriteria)
        {
            return memberInfo.GetCustomAttributes((Type) filterCriteria).Any();
        }
    }
}