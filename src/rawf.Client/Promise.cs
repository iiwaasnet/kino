using System;
using System.Threading.Tasks;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;

namespace rawf.Client
{
    public class Promise : IPromise
    {
        private readonly TaskCompletionSource<IMessage> result;
        private static readonly TimeSpan DefaultPromiseExpiration = TimeSpan.FromSeconds(2);

        public Promise(TimeSpan expiresAfter)
        {
            result = new TaskCompletionSource<IMessage>();
            ExpireAfter = expiresAfter;
        }

        public Promise()
            : this(DefaultPromiseExpiration)
        {
        }

        public Task<IMessage> GetResponse()
            => result.Task;

        internal void SetResult(IMessage message)
        {
            if (Unsafe.Equals(message.Identity, ExceptionMessage.MessageIdentity))
            {
                var error = message.GetPayload<ExceptionMessage>().Exception;
                result.SetException(error);
            }
            else
            {
                result.SetResult(message);
            }
        }

        public TimeSpan ExpireAfter { get; }
    }
}