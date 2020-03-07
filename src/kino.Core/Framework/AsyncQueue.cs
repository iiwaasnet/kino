using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace kino.Core.Framework
{
    public class AsyncQueue<T> : IAsyncQueue<T>
    {
        private readonly BlockingCollection<T> asyncResponses;

        public AsyncQueue()
            => asyncResponses = new BlockingCollection<T>(new ConcurrentQueue<T>());

        public IEnumerable<T> GetConsumingEnumerable(CancellationToken cancellationToken)
            => asyncResponses.GetConsumingEnumerable(cancellationToken);

        public void Enqueue(T messageCompletion, CancellationToken cancellationToken)
            => asyncResponses.Add(messageCompletion, cancellationToken);

        public void Dispose()
            => asyncResponses.Dispose();
    }
}