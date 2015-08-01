using System.Collections.Generic;
using System.Linq;

namespace rawf.Connectivity
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever

    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly IDictionary<MessageHandlerIdentifier, HashSet<SocketIdentifier>> map;

        public InternalRoutingTable()
        {
            map = new Dictionary<MessageHandlerIdentifier, HashSet<SocketIdentifier>>();
        }

        public void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashSet<SocketIdentifier> hashSet;
            if (!map.TryGetValue(messageHandlerIdentifier, out hashSet))
            {
                hashSet = new HashSet<SocketIdentifier>();
                map[messageHandlerIdentifier] = hashSet;
            }
            hashSet.Add(socketIdentifier);
        }

        public SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            //TODO: Implement round robin
            HashSet<SocketIdentifier> collection;
            return map.TryGetValue(messageHandlerIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        private static T Get<T>(ICollection<T> hashSet)
        {
            return hashSet.Any()
                       ? hashSet.First()
                       : default(T);
        }

        public IEnumerable<MessageHandlerIdentifier> GetMessageHandlerIdentifiers()
        {
            return map.Keys;
        }
    }
}