using System;

namespace rawf.Framework
{
    public interface IExpirableItemCollection<T> : IDisposable
    {
        void SetExpirationHandler(Action<T> handler);
        IExpirableItem Delay(T item, TimeSpan expireAfter);
    }
}