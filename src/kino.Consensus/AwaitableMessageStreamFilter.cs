using System;
using System.Collections.Generic;
using System.Threading;
using kino.Consensus.Messages;
using kino.Core.Messaging;

namespace kino.Consensus
{
    public class AwaitableMessageStreamFilter : IObserver<IMessage>, IDisposable
    {
        private readonly Func<IMessage, bool> predicate;
        private readonly Func<IMessage, ILeaseMessage> payload;
        private readonly int maxCount;
        private int currentCount;
        private readonly ManualResetEventSlim awaitable;
        private readonly IDictionary<string, IMessage> messages;
        private readonly object locker = new object();

        public AwaitableMessageStreamFilter(Func<IMessage, bool> predicate, Func<IMessage, ILeaseMessage> payload, int maxCount)
        {
            this.predicate = predicate;
            this.maxCount = maxCount;
            this.payload = payload;
            currentCount = 0;
            messages = new Dictionary<string, IMessage>();
            awaitable = new ManualResetEventSlim(false);
        }

        public void OnNext(IMessage value)
        {
            if (predicate(value))
            {
                var messagePayload = payload(value); 
                lock (locker)
                {
                    if (!awaitable.IsSet)
                    {
                        if (!messages.ContainsKey(messagePayload.SenderUri))
                        {
                            messages[messagePayload.SenderUri] = value;
                            currentCount++;
                        }
                    }
                    if (currentCount == maxCount && !awaitable.IsSet)
                    {
                        awaitable.Set();
                    }
                }
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        public void Dispose()
        {
            awaitable.Dispose();
        }

        public WaitHandle Filtered => awaitable.WaitHandle;

        public IEnumerable<IMessage> MessageStream
        {
            get
            {
                awaitable.Wait();

                return messages.Values;
            }
        }
    }
}