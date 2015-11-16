using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Client
{
    public class CallbackHandlerStack : ICallbackHandlerStack
    {
        private readonly ConcurrentDictionary<CorrelationId, IDictionary<MessageIdentifier, IPromise>> handlers;

        public CallbackHandlerStack()
        {
            handlers = new ConcurrentDictionary<CorrelationId, IDictionary<MessageIdentifier, IPromise>>();
        }

        public void Push(CorrelationId correlation, IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            if (handlers.TryAdd(correlation, messageIdentifiers.ToDictionary(mp => mp, mp => promise)))
            {
                ((Promise) promise).SetRemoveCallbackHandler(correlation, RemoveCallback);
            }
            else
            {
                throw new DuplicatedKeyException($"Duplicated key: Correlation[{correlation.Value.GetString()}]");
            }
        }

        public IPromise Pop(CallbackHandlerKey callbackIdentifier)
        {
            IPromise promise = null;

            IDictionary<MessageIdentifier, IPromise> messageHandlers;
            if (handlers.TryRemove(new CorrelationId(callbackIdentifier.Correlation), out messageHandlers))
            {
                var massageHandlerId = new MessageIdentifier(callbackIdentifier.Version, callbackIdentifier.Identity);
                messageHandlers.TryGetValue(massageHandlerId, out promise);
            }

            return promise;
        }

        private void RemoveCallback(CorrelationId correlationId)
        {
            IDictionary<MessageIdentifier, IPromise> _;
            handlers.TryRemove(correlationId, out _);
        }
    }
}