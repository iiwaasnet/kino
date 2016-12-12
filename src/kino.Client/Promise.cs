using System;
using System.Threading.Tasks;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Client
{
    internal class Promise : IPromise
    {
        private readonly TaskCompletionSource<IMessage> result;
        private volatile Action<CallbackKey> removeCallbackHandler;
        private volatile bool isDisposed;

        internal Promise(long callbackKey)
        {
            isDisposed = false;
            result = new TaskCompletionSource<IMessage>();
            CallbackKey = new CallbackKey(callbackKey);
        }

        public Task<IMessage> GetResponse()
        {
            if (!isDisposed)
            {
                return result.Task;
            }

            throw new ObjectDisposedException("Promise");
        }

        internal void SetResult(IMessage message)
        {
            RemoveCallbackHander();

            if (message.Equals(KinoMessages.Exception))
            {
                var error = message.GetPayload<ExceptionMessage>().Exception;
                result.TrySetException(error);
            }
            else
            {
                result.TrySetResult(message);
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                RemoveCallbackHander();
            }
        }

        private void RemoveCallbackHander()
        {
            var tmp = removeCallbackHandler;
            if (tmp != null)
            {
                removeCallbackHandler = null;
                tmp(CallbackKey);
            }
        }

        internal void SetRemoveCallbackHandler(Action<CallbackKey> removeCallbackHandler)
            => this.removeCallbackHandler = removeCallbackHandler;

        public CallbackKey CallbackKey { get; }
    }
}