using System.Collections.Generic;
using kino.Core;
using kino.Core.Framework;

namespace kino.Routing.ServiceMessageHandlers
{
    public class ServiceMessageHandlerRegistry : IServiceMessageHandlerRegistry
    {
        private readonly IDictionary<MessageIdentifier, IServiceMessageHandler> handlersMap;

        public ServiceMessageHandlerRegistry(IEnumerable<IServiceMessageHandler> serviceMessageHandlers)
            => handlersMap = CreateHandlersMap(serviceMessageHandlers);

        private static IDictionary<MessageIdentifier, IServiceMessageHandler> CreateHandlersMap(IEnumerable<IServiceMessageHandler> serviceMessageHandlers)
        {
            var tmp = new Dictionary<MessageIdentifier, IServiceMessageHandler>();
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

        public IServiceMessageHandler GetMessageHandler(MessageIdentifier message)
            => handlersMap.TryGetValue(message, out var handler)
                   ? handler
                   : null;
    }
}