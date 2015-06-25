using System;
using System.Collections.Concurrent;
using Console.Messages;

namespace Console
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever
    internal class CallbackHandlerStack
    {
        private readonly ConcurrentDictionary<MessageIdentifier, IPromise> handlers;

        public CallbackHandlerStack()
        {
            handlers =  new ConcurrentDictionary<MessageIdentifier, IPromise>();
        }

        internal void Push(MessageIdentifier messageIdentifier, IPromise promise)
        {
            if (handlers.ContainsKey(messageIdentifier))
            {
                throw new Exception($"Duplicated key: MessageIdentity[{messageIdentifier.MessageIdentity.GetString()}]-ReceiverIdentity[{messageIdentifier.ReceiverIdentity.GetString()}]");
            }
            handlers[messageIdentifier] = promise;
        }

        internal IPromise Pop(MessageIdentifier messageIdentifier)
        {
            IPromise promise;
            handlers.TryRemove(messageIdentifier, out promise);

            return promise;
        }
    }
}