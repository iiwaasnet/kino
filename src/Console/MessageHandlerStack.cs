using System.Collections.Generic;
using System.Linq;

namespace Console
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever
    internal class MessageHandlerStack
    {
        private readonly IDictionary<MessageIdentifier, HashSet<SocketIdentifier>> storage;

        internal MessageHandlerStack()
        {
            storage = new Dictionary<MessageIdentifier, HashSet<SocketIdentifier>>();
        }

        internal void Push(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashSet<SocketIdentifier> collection;
            if (!storage.TryGetValue(messageIdentifier, out collection))
            {
                collection = new HashSet<SocketIdentifier>();
                storage[messageIdentifier] = collection;
            }
            Push(collection, socketIdentifier);
        }

        internal SocketIdentifier Pop(MessageIdentifier messageIdentifier)
        {
            HashSet<SocketIdentifier> collection;
            return storage.TryGetValue(messageIdentifier, out collection)
                       ? Pop(collection)
                       : null;
        }

        private static void Push<T>(ISet<T> hashSet, T element)
        {
            hashSet.Add(element);
        }

        private static T Pop<T>(ICollection<T> hashSet)
        {
            var el = hashSet.First();
            hashSet.Remove(el);

            return el;
        }
    }
}