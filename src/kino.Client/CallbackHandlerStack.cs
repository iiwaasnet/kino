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
        private readonly ConcurrentDictionary<long, IDictionary<MessageIdentifier, IPromise>> keyPromiseMap;

        public CallbackHandlerStack()
        {
            keyPromiseMap = new ConcurrentDictionary<long, IDictionary<MessageIdentifier, IPromise>>();
        }

        public void Push(IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers)
        {
            if (keyPromiseMap.TryAdd(promise.CallbackKey.Value, messageIdentifiers.ToDictionary(mp => mp, mp => promise)))
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

            if (keyPromiseMap.TryRemove(callbackIdentifier.CallbackKey, out var messageHandlers))
            {
                var massageHandlerId = new MessageIdentifier(callbackIdentifier.Identity, callbackIdentifier.Version, callbackIdentifier.Partition);
                messageHandlers.TryGetValue(massageHandlerId, out promise);
            }

            return promise;
        }

        private void RemoveCallback(CallbackKey callbackKey)
            => keyPromiseMap.TryRemove(callbackKey.Value, out var _);
    }
}