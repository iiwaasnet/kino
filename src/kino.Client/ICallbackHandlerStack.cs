using System.Collections.Generic;
using kino.Connectivity;
using kino.Messaging;

namespace kino.Client
{
    public interface ICallbackHandlerStack
    {
        void Push(CorrelationId correlation, IPromise promise, IEnumerable<IMessageIdentifier> messageIdentifiers);
        IPromise Pop(CallbackHandlerKey callbackIdentifier);
    }
}