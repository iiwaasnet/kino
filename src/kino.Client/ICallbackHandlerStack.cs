using System.Collections.Generic;
using kino.Core;

namespace kino.Client
{
    public interface ICallbackHandlerStack
    {
        void Push(long callbackKey, IPromise promise, IEnumerable<MessageIdentifier> messageIdentifiers);

        IPromise Pop(CallbackHandlerKey callbackIdentifier);
    }
}