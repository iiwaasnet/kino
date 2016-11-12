using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Client
{
    public class CallbackHandlerStack : ICallbackHandlerStack
    {
        private readonly ConcurrentDictionary<long, IDictionary<MessageIdentifier, IPromise>> handlers;

        public CallbackHandlerStack()
        {
            handlers = new ConcurrentDictionary<long, IDictionary<MessageIdentifier, IPromise>>();
        }

        public void Push(long callbackKey, IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            if (handlers.TryAdd(callbackKey, messageIdentifiers.ToDictionary(mp => mp, mp => promise)))
            {
                ((Promise) promise).SetRemoveCallbackHandler(callbackKey, RemoveCallback);
            }
            else
            {
                throw new DuplicatedKeyException($"Duplicated {nameof(callbackKey)} [{callbackKey}]");
            }
        }

        public IPromise Pop(CallbackHandlerKey callbackIdentifier)
        {
            IPromise promise = null;

            IDictionary<MessageIdentifier, IPromise> messageHandlers;
            if (handlers.TryRemove(callbackIdentifier.CallbackKey, out messageHandlers))
            {
                var massageHandlerId = new MessageIdentifier(callbackIdentifier.Identity, callbackIdentifier.Version, callbackIdentifier.Partition);
                messageHandlers.TryGetValue(massageHandlerId, out promise);
            }

            return promise;
        }

        private void RemoveCallback(CallbackKey callbackKey)
        {
            IDictionary<MessageIdentifier, IPromise> _;
            handlers.TryRemove(callbackKey.Value, out _);
        }
    }
}