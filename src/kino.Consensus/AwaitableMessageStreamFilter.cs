using System;
using System.Collections.Generic;
using System.Threading;
using kino.Consensus.Messages;
using kino.Messaging;

namespace kino.Consensus
{
    public class AwaitableMessageStreamFilter : IObserver<IMessage>, IDisposable
    {
        private readonly Func<IMessage, bool> predicate;
        private readonly Func<IMessage, ILeaseMessage> payload;
        private readonly int maxCount;
        private int currentCount;
        private readonly ManualResetEvent awaitable;
        private readonly IDictionary<string, IMessage> messages;
        private readonly object locker = new object();

        public AwaitableMessageStreamFilter(Func<IMessage, bool> predicate, Func<IMessage, ILeaseMessage> payload, int maxCount)
        {
            this.predicate = predicate;
            this.maxCount = maxCount;
            this.payload = payload;
            currentCount = 0;
            messages = new Dictionary<string, IMessage>();
            awaitable = new ManualResetEvent(false);
        }

        public void OnNext(IMessage value)
        {
            if (predicate(value))
            {
                var messagePayload = payload(value);
                lock (locker)
                {
                    if (!awaitable.WaitOne(TimeSpan.Zero))
                    {
                        if (!messages.ContainsKey(messagePayload.SenderUri))
                        {
                            messages[messagePayload.SenderUri] = value;
                            currentCount++;
                        }
                    }
                    if (currentCount == maxCount && !awaitable.WaitOne(TimeSpan.Zero))
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

        public WaitHandle Filtered => awaitable;

        public IEnumerable<IMessage> MessageStream
        {
            get
            {
                awaitable.WaitOne();

                return messages.Values;
            }
        }
    }
}