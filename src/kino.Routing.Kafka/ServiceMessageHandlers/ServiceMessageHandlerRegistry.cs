using System.Collections.Generic;
using kino.Core;
using kino.Core.Framework;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public class ServiceMessageHandlerRegistry : IKafkaServiceMessageHandlerRegistry
    {
        private readonly IDictionary<MessageIdentifier, IKafkaServiceMessageHandler> handlersMap;

        public ServiceMessageHandlerRegistry(IEnumerable<IKafkaServiceMessageHandler> serviceMessageHandlers)
            => handlersMap = CreateHandlersMap(serviceMessageHandlers);

        private static IDictionary<MessageIdentifier, IKafkaServiceMessageHandler> CreateHandlersMap(IEnumerable<IKafkaServiceMessageHandler> serviceMessageHandlers)
        {
            var tmp = new Dictionary<MessageIdentifier, IKafkaServiceMessageHandler>();
            foreach (var messageHandler in serviceMessageHandlers)
            {
                if (!tmp.ContainsKey(messageHandler.TargetMessage))
                {
                    tmp[messageHandler.TargetMessage] = messageHandler;
                }
                else
                {
                    throw new DuplicatedKeyException($"Message {messageHandler.TargetMessage} is already mapped to handler!");
                }
            }

            return tmp;
        }

        public IKafkaServiceMessageHandler GetMessageHandler(MessageIdentifier message)
            => handlersMap.TryGetValue(message, out var handler)
                   ? handler
                   : null;
    }
}