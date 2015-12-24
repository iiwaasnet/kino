using System;
using System.Threading.Tasks;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;

namespace kino.Client
{
    internal class Promise : IPromise
    {
        private readonly TaskCompletionSource<IMessage> result;
        private volatile Action<CorrelationId> removeCallbackHandler;
        private volatile bool isDisposed;

        internal Promise()
        {
            isDisposed = false;
            result = new TaskCompletionSource<IMessage>();
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
                tmp(CorrelationId);
            }
        }

        internal void SetRemoveCallbackHandler(CorrelationId correlationId, Action<CorrelationId> removeCallbackHandler)
        {
            CorrelationId = correlationId;
            this.removeCallbackHandler = removeCallbackHandler;
        }

        public CorrelationId CorrelationId { get; private set; }
    }
}