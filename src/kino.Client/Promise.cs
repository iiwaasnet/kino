using System;
using System.Threading.Tasks;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Client
{
    internal class Promise : IPromise
    {
        private readonly TaskCompletionSource<IMessage> result;
        private static readonly TimeSpan DefaultPromiseExpiration = TimeSpan.FromSeconds(20);
        private IExpirableItem expirableItem;

        internal Promise(TimeSpan expiresAfter)
        {
            result = new TaskCompletionSource<IMessage>();
            ExpireAfter = expiresAfter;
        }

        internal Promise()
            : this(DefaultPromiseExpiration)
        {
        }

        public Task<IMessage> GetResponse()
            => result.Task;

        internal void SetResult(IMessage message)
        {
            expirableItem?.ExpireNow();

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

        internal void SetExpiration(IExpirableItem expirableItem)
            => this.expirableItem = expirableItem;

        internal void SetExpired()
            => result.SetException(new TimeoutException());

        public TimeSpan ExpireAfter { get; }
    }
}