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

        public void Push(IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            if (handlers.TryAdd(promise.CallbackKey.Value, messageIdentifiers.ToDictionary(mp => mp, mp => promise)))
            {
                ((Promise) promise).SetRemoveCallbackHandler(RemoveCallback);
            }
            else
            {
                throw new DuplicatedKeyException($"Duplicated {nameof(promise.CallbackKey)} [{promise.CallbackKey.Value}]");
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