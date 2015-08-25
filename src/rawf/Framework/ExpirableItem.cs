using System;

namespace rawf.Framework
{
    internal class ExpirableItem<T> : IExpirableItem, IComparable<ExpirableItem<T>>
    {
        private readonly DateTime expirationTime;

        internal ExpirableItem(T item, TimeSpan expireAfter)
        {
            Item = item;
            expirationTime = DateTime.UtcNow + expireAfter;
            ExpireAfter = expireAfter.Milliseconds;
        }

        public void ExpireNow()
        {
            ExpireAfter = 0;
        }

        public int CompareTo(ExpirableItem<T> other)
        {
            return ExpireAfter.CompareTo(other.ExpireAfter);
        }

        internal bool IsExpired(DateTime now)
            => now >= expirationTime;

        internal T Item { get; }
        internal int ExpireAfter { get; private set; }
    }
}