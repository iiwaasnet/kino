using System;

namespace rawf.Framework
{
    public interface IDelayedCollection<T> : IDisposable
    {
        void SetExpirationHandler(Action<T> handler);
        IDelayedItem Delay(T item, TimeSpan expireAfter);
    }
}