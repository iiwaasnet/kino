using System.Collections.Generic;
using kino.Connectivity;
using kino.Messaging;

namespace kino.Client
{
    public interface ICallbackHandlerStack
    {
        void Push(CorrelationId correlation, IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers);
        IPromise Pop(CallbackHandlerKey callbackIdentifier);
    }
}