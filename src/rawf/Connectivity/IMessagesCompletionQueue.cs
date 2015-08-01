using System;
using System.Collections.Generic;
using System.Threading;

namespace rawf.Connectivity
{
    public interface IMessagesCompletionQueue : IDisposable
    {
        IEnumerable<AsyncMessageContext> GetMessages(CancellationToken cancellationToken);
        void Enqueue(AsyncMessageContext messageCompletion, CancellationToken cancellationToken);
    }
}