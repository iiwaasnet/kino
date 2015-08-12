using System;

namespace rawf.Framework
{
    public partial class DelayedCollection<T>
    {
        private class DelayedItem : IDelayedItem
        {
            private readonly DelayedCollection<T> storage;
            private readonly DateTime timeAdded;

            internal DelayedItem(T item, TimeSpan expireAfter, DelayedCollection<T> storage)
            {
                this.storage = storage;
                Item = item;
                timeAdded = DateTime.UtcNow;
                ExpireAfter = expireAfter.Milliseconds;
            }

            internal T Item { get; }
            internal int ExpireAfter { get; private set; }

            public void ExpireNow()
            {
                ExpireAfter = 0;
                storage.TriggerDelayEvaluation();
            }

            internal bool IsExpired(DateTime now)
                => now >= timeAdded.AddMilliseconds(ExpireAfter);
        }
    }
}