using System;

namespace rawf.Framework
{
    public partial class ExpirableItemCollection<T>
    {
        private class ExpirableItem : IExpirableItem
        {
            private readonly DateTime expirationTime;

            internal ExpirableItem(T item, TimeSpan expireAfter)
            {
                Item = item;
                expirationTime = DateTime.UtcNow + expireAfter;
                ExpireAfter = expireAfter.Milliseconds;
            }

            internal T Item { get; }
            internal int ExpireAfter { get; private set; }

            public void ExpireNow()
            {
                ExpireAfter = 0;
            }

            internal bool IsExpired(DateTime now)
                => now >= expirationTime;
        }
    }
}