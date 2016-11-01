using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Client
{
    public class CallbackPoint
    {
        public CallbackPoint(params MessageIdentifier[] messageIdentifiers)
        {
            MessageIdentifiers = messageIdentifiers;
        }

        public static CallbackPoint Create<T>(byte[] partition = null)
            where T : IIdentifier, new()
        => new CallbackPoint(MessageIdentifier.Create<T>(partition));

        public static CallbackPoint Create<T1, T2>(byte[] partition = null)
            where T1 : IIdentifier, new()
            where T2 : IIdentifier, new()
        => new CallbackPoint(MessageIdentifier.Create<T1>(partition),
                             MessageIdentifier.Create<T2>(partition));

        public static CallbackPoint Create<T1, T2, T3>(byte[] partition = null)
            where T1 : IIdentifier, new()
            where T2 : IIdentifier, new()
            where T3 : IIdentifier, new()
        => new CallbackPoint(MessageIdentifier.Create<T1>(partition),
                             MessageIdentifier.Create<T2>(partition),
                             MessageIdentifier.Create<T3>(partition));

        public IEnumerable<MessageIdentifier> MessageIdentifiers { get; }
    }
}