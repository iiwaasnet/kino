using System;

namespace kino.Framework
{
    public interface IExpirableItemCollection<T> : IDisposable
    {
        void SetExpirationHandler(Action<T> handler);
        IExpirableItem Delay(T item, TimeSpan expireAfter);
    }
}