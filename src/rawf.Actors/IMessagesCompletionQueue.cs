using System;
using System.Collections.Generic;
using System.Threading;
using rawf.Connectivity;

namespace rawf.Actors
{
    public interface IMessagesCompletionQueue : IDisposable
    {
        IEnumerable<AsyncMessageContext> GetMessages(CancellationToken cancellationToken);
        void Enqueue(AsyncMessageContext messageCompletion, CancellationToken cancellationToken);
    }
}