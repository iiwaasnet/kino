using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Framework;
using kino.Messaging;

namespace kino.Client
{
    public class CallbackHandlerStack : ICallbackHandlerStack
    {
        private readonly ConcurrentDictionary<CorrelationId, IDictionary<MessageIdentifier, IPromise>> handlers;
        private readonly IExpirableItemCollection<CorrelationId> expirationQueue;

        public CallbackHandlerStack(IExpirableItemCollection<CorrelationId> expirationQueue)
        {
            handlers =  new ConcurrentDictionary<CorrelationId, IDictionary<MessageIdentifier, IPromise>>();
            expirationQueue.SetExpirationHandler(RemoveExpiredCallback);
            this.expirationQueue = expirationQueue;
        }

        private void RemoveExpiredCallback(CorrelationId correlationId)
        {
            IDictionary<MessageIdentifier, IPromise> value;
            if (handlers.TryRemove(correlationId, out value))
            {
                ((Promise)value.Values.First()).SetExpired();
            }
        }

        public void Push(CorrelationId correlation, IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            IDictionary<MessageIdentifier, IPromise> messageHandlers;
            if (handlers.TryGetValue(correlation, out messageHandlers))
            {
                throw new DuplicatedKeyException($"Duplicated key: Correlation[{correlation.Value.GetString()}]");
            }

            var delayedItem = expirationQueue.Delay(correlation, promise.ExpireAfter);
            ((Promise)promise).SetExpiration(delayedItem);

            handlers[correlation] = messageIdentifiers.ToDictionary(mp => mp, mp => promise);
        }

        public IPromise Pop(CallbackHandlerKey callbackIdentifier)
        {
            IPromise promise = null;
            
            IDictionary<MessageIdentifier, IPromise> messageHandlers;
            if(handlers.TryRemove(new CorrelationId(callbackIdentifier.Correlation), out messageHandlers))
            {
                var massageHandlerId = new MessageIdentifier(callbackIdentifier.Version, callbackIdentifier.Identity);
                messageHandlers.TryGetValue(massageHandlerId, out promise);
            }
            
            return promise;
        }
    }
}