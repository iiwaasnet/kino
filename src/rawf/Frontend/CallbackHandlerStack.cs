using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Frontend
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever
    public class CallbackHandlerStack : ICallbackHandlerStack
    {
        private readonly ConcurrentDictionary<CorrelationId, IDictionary<MessageHandlerIdentifier, IPromise>> handlers;
        
        public CallbackHandlerStack()
        {
            handlers =  new ConcurrentDictionary<CorrelationId, IDictionary<MessageHandlerIdentifier, IPromise>>();
        }

        public void Push(CorrelationId correlation, IPromise promise, IEnumerable<MessageHandlerIdentifier> messageHandlerIdentifiers)
        {
            IDictionary<MessageHandlerIdentifier, IPromise> messageHandlers;
            if (handlers.TryGetValue(correlation, out messageHandlers))
            {
                throw new DuplicatedKeyException($"Duplicated key: Correlation[{correlation.Value.GetString()}]");
            }
            handlers[correlation] = messageHandlerIdentifiers.ToDictionary(mp => mp, mp => promise);
        }

        public IPromise Pop(CallbackHandlerKey callbackIdentifier)
        {
            IPromise promise = null;
            
            IDictionary<MessageHandlerIdentifier, IPromise> messageHandlers;
            if(handlers.TryRemove(new CorrelationId(callbackIdentifier.Correlation), out messageHandlers))
            {
                var massageHandlerId = new MessageHandlerIdentifier(callbackIdentifier.Version, callbackIdentifier.Identity);
                messageHandlers.TryGetValue(massageHandlerId, out promise);
            }
            
            return promise;
        }
    }
}