using System.Collections.Generic;
using kino.Core.Connectivity;
using kino.Core.Messaging;

namespace kino.Client
{
    public interface ICallbackHandlerStack
    {
        void Push(CorrelationId correlation, IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers);
        IPromise Pop(CallbackHandlerKey callbackIdentifier);
    }
}