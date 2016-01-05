using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using kino.Core.Connectivity;
using kino.Core.Framework;

namespace kino.Actors
{
    public class ActorHandlerMap : IActorHandlerMap
    {
        private readonly IDictionary<MessageIdentifier, MessageHandler> messageHandlers;
        private readonly object @lock = new object();

        public ActorHandlerMap()
        {
            messageHandlers = new Dictionary<MessageIdentifier, MessageHandler>();
        }

        public IEnumerable<MessageIdentifier> Add(IActor actor)
        {
            var tmp = new List<MessageIdentifier>();
            var registrations = GetActorRegistrations(actor);

            lock (@lock)
            {
                AssertRegistrationsNotDiplicated(registrations);

                foreach (var reg in registrations)
                {
                    messageHandlers.Add(reg.Key, reg.Value);

                    tmp.Add(reg.Key);
                }
            }

            if (!tmp.Any())
            {
                throw new Exception($"Actor {actor.GetType().FullName} seems to not handle any message!");
            }

            return tmp;
        }

        private void AssertRegistrationsNotDiplicated(IEnumerable<KeyValuePair<MessageIdentifier, MessageHandler>> registrations)
        {
            var conflict = registrations.Select(reg => reg.Key).FirstOrDefault(key => messageHandlers.ContainsKey(key));

            if (conflict != null)
            {
                throw new DuplicatedKeyException(conflict.ToString());
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

        public IEnumerable<MessageIdentifier> GetMessageHandlerIdentifiers()
        {
            return messageHandlers.Keys;
        }

        private static IEnumerable<KeyValuePair<MessageIdentifier, MessageHandler>> GetActorRegistrations(IActor actor)
            => actor
                .GetInterfaceDefinition()
                .Select(messageMap =>
                        new KeyValuePair<MessageIdentifier, MessageHandler>(new MessageIdentifier(messageMap.Message.Version,
                                                                                                  messageMap.Message.Identity),
                                                                            messageMap.Handler));

        private static bool InterfaceMethodFilter(MemberInfo memberInfo, object filterCriteria)
        {
            return memberInfo.GetCustomAttributes((Type) filterCriteria).Any();
        }
    }
}