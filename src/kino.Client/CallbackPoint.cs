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

        public static ICallbackPoint Create<T>()
            where T : IMessageIdentifier, new()
        {
            var message = new T();
            return new CallbackPoint(new MessageIdentifier(message.Version, message.Identity, message.Partition));
        }

        public static ICallbackPoint Create<T1, T2>()
            where T1 : IMessageIdentifier, new()
            where T2 : IMessageIdentifier, new()
        {
            var message1 = new T1();
            var message2 = new T2();
            return new CallbackPoint(new MessageIdentifier(message1),
                                     new MessageIdentifier(message2));
        }

        public static ICallbackPoint Create<T1, T2, T3>()
            where T1 : IMessageIdentifier, new()
            where T2 : IMessageIdentifier, new()
            where T3 : IMessageIdentifier, new()
        {
            var message1 = new T1();
            var message2 = new T2();
            var message3 = new T3();
            return new CallbackPoint(new MessageIdentifier(message1),
                                     new MessageIdentifier(message2),
                                     new MessageIdentifier(message3));
        }

        public IEnumerable<MessageIdentifier> MessageIdentifiers { get; }
    }
}