using System;

namespace rawf.Framework
{
    public partial class DelayedCollection<T>
    {
        private class ExpirableItem : IExpirableItem
        {
            private readonly DelayedCollection<T> storage;
            private readonly DateTime expirationTime;

            internal ExpirableItem(T item, TimeSpan expireAfter, DelayedCollection<T> storage)
            {
                this.storage = storage;
                Item = item;
                expirationTime = DateTime.UtcNow + expireAfter;
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
                => now >= expirationTime;
        }
    }
}