using System.Collections.Generic;
using rawf.Messaging;

namespace rawf.Client
{
    public interface ICallbackHandlerStack
    {
        void Push(CorrelationId correlation, IPromise promise, IEnumerable<MessageHandlerIdentifier> messageHandlerIdentifiers);
        IPromise Pop(CallbackHandlerKey callbackIdentifier);
    }
}