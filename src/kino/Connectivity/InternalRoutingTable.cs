using System.Collections.Generic;
using System.Linq;
using C5;

namespace kino.Connectivity
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever

    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>> map;

        public InternalRoutingTable()
        {
            map = new Dictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>>();
        }

        public void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (!map.TryGetValue(messageHandlerIdentifier, out hashSet))
            {
                hashSet = new HashedLinkedList<SocketIdentifier>();
                map[messageHandlerIdentifier] = hashSet;
            }
            if (!hashSet.Contains(socketIdentifier))
            {
                hashSet.InsertLast(socketIdentifier);
            }
        }

        public SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return map.TryGetValue(messageHandlerIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> PopAll(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return map.TryGetValue(messageHandlerIdentifier, out collection)
                       ? collection
                       : Enumerable.Empty<SocketIdentifier>();
        }

        private static T Get<T>(HashedLinkedList<T> hashSet)
        {
            if (hashSet.Any())
            {
                var first = hashSet.RemoveFirst();
                hashSet.InsertLast(first);
                return first;
            }

            return default(T);
        }

        public IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers()
            => map.Keys;

        public int Remove(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            var handlersCount = 0;
            HashedLinkedList<SocketIdentifier> hashSet;
            if (map.TryGetValue(messageHandlerIdentifier, out hashSet))
            {
                hashSet.Remove(socketIdentifier);
                handlersCount = hashSet.Count;
                if (handlersCount == 0)
                {
                    map.Remove(messageHandlerIdentifier);
                }
            }

            return handlersCount;
        }
    }
}