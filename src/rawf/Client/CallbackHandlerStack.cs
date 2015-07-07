using System;
using System.Collections.Concurrent;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Client
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever
    internal class CallbackHandlerStack
    {
        private readonly ConcurrentDictionary<CallbackHandlerKey, IPromise> handlers;

        public CallbackHandlerStack()
        {
            handlers =  new ConcurrentDictionary<CallbackHandlerKey, IPromise>();
        }

        internal void Push(CallbackHandlerKey callbackIdentifier, IPromise promise)
        {
            if (handlers.ContainsKey(callbackIdentifier))
            {
                //TODO: Improve by implementing ToString()
                throw new Exception($"Duplicated key: ReceiverIdentity[{callbackIdentifier}]-Version[{callbackIdentifier.Version.GetString()}]");
            }
            handlers[callbackIdentifier] = promise;
        }

        internal IPromise Pop(CallbackHandlerKey callbackIdentifier)
        {
            IPromise promise;
            handlers.TryRemove(callbackIdentifier, out promise);

            return promise;
        }
    }
}