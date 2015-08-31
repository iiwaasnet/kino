using System;
using System.Collections.Generic;
using System.Threading;

namespace kino.Framework
{
    public interface IAsyncQueue<T> : IDisposable
    {
        IEnumerable<T> GetConsumingEnumerable(CancellationToken cancellationToken);
        void Enqueue(T messageCompletion, CancellationToken cancellationToken);
    }
}