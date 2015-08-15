using System;
using System.Collections.Generic;
using System.Threading;

namespace rawf.Framework
{
    public interface IAsyncQueue<T> : IDisposable
    {
        //IEnumerable<AsyncMessageContext> GetMessages(CancellationToken cancellationToken);
        //void Enqueue(AsyncMessageContext messageCompletion, CancellationToken cancellationToken);
        IEnumerable<T> GetMessages(CancellationToken cancellationToken);
        void Enqueue(T messageCompletion, CancellationToken cancellationToken);
    }
}