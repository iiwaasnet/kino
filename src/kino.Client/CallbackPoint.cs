using System.Collections.Generic;
using kino.Core.Connectivity;
using kino.Core.Messaging;

namespace kino.Client
{
    public class CallbackPoint : ICallbackPoint
    {
        public CallbackPoint(params MessageIdentifier[] messageIdentifiers)
        {
            MessageIdentifiers = messageIdentifiers;
        }

        public static ICallbackPoint Create<T>(byte[] partition = null)
            where T : IMessageIdentifier, new()
            => new CallbackPoint(MessageIdentifier.Create<T>(partition));

        public static ICallbackPoint Create<T1, T2>(byte[] partition = null)
            where T1 : IMessageIdentifier, new()
            where T2 : IMessageIdentifier, new()
            => new CallbackPoint(MessageIdentifier.Create<T1>(partition),
                                 MessageIdentifier.Create<T2>(partition));

        public static ICallbackPoint Create<T1, T2, T3>(byte[] partition = null)
            where T1 : IMessageIdentifier, new()
            where T2 : IMessageIdentifier, new()
            where T3 : IMessageIdentifier, new()
            => new CallbackPoint(MessageIdentifier.Create<T1>(partition),
                                 MessageIdentifier.Create<T2>(partition),
                                 MessageIdentifier.Create<T3>(partition));

        public IEnumerable<MessageIdentifier> MessageIdentifiers { get; }
    }
}