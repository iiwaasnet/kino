using System;
using System.Collections.Concurrent;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Client
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever
    internal class CallbackHandlerStack
    {
        private readonly ConcurrentDictionary<CorrelationId, ConcurrentDictionary<MessageHandlerIdentifier, IPromise>> handlers;
        
        public CallbackHandlerStack()
        {
            handlers =  new ConcurrentDictionary<CorrelationId, ConcurrentDictionary<MessageHandlerIdentifier, IPromise>>();
        }

        internal void Push(CallbackHandlerKey callbackIdentifier, IPromise promise)
        {
            var correlation = new CorrelationId(callbackIdentifier.Correlation)
            ConcurrentDictionary<MessageHandlerIdentifier, IPromise> messageHandlers;
            if (!handlers.TryGetValue(correlation, out messageHandlers))
            {
                messagehandlers = new ConcurrentDictionary<MessageHandlerIdentifier, IPromise>();
                handlers[correlation] = messageHandlers;                
            }
            var massageHandlerId = new MessageHandlerIdentifier(callbackIdentifier.Version, callbackIdentifier.Identity);
            if (messageHandlers.ContainsValue(messagehandlerId))
            {
                //TODO: Improve by implementing ToString()
                throw new Exception($"Duplicated key: Identity[{massageHandlerId.Identity.GetString()}]-Version[{massageHandlerId.Version.GetString()}]");
            }
            messageHandlers[messageHandlerId] = promise;
        }

        internal IPromise Pop(CallbackHandlerKey callbackIdentifier)
        {
            IPromise promise = null;
            
            ConcurrentDictionary<MessageHandlerIdentifier, IPromise> messageHandlers;
            if(handlers.TryRemove(new CorrelationId(callbackIdentifier.Correlation)))
            {
                var massageHandlerId = new MessageHandlerIdentifier(callbackIdentifier.Version, callbackIdentifier.Identity);
                messageHandlers.TryGetValue(messageHandlerId, out promise);
            }
            
            return promise;
        }
    }
}