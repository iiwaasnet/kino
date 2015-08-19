using System;
using System.Collections.Generic;
using System.Threading;

namespace rawf.Framework
{
    public interface IAsyncQueue<T> : IDisposable
    {
        IEnumerable<T> GetConsumingEnumerable(CancellationToken cancellationToken);
        void Enqueue(T messageCompletion, CancellationToken cancellationToken);
    }
}