using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<Identifier, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly System.Collections.Generic.IDictionary<SocketIdentifier, HashedLinkedList<Identifier>> socketToMessageMap;

        public InternalRoutingTable()
        {
            messageToSocketMap = new Dictionary<Identifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new Dictionary<SocketIdentifier, HashedLinkedList<Identifier>>();
        }

        public void AddMessageRoute(Identifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            MapHandlerToSocket(messageIdentifier, socketIdentifier);
            MapSocketToHandler(messageIdentifier, socketIdentifier);
        }

        private void MapSocketToHandler(Identifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<Identifier> handlers;
            if (!socketToMessageMap.TryGetValue(socketIdentifier, out handlers))
            {
                handlers = new HashedLinkedList<Identifier>();
                socketToMessageMap[socketIdentifier] = handlers;
            }
            if (!handlers.Contains(messageIdentifier))
            {
                handlers.InsertLast(messageIdentifier);
            }
        }

        private void MapHandlerToSocket(Identifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> sockets;
            if (!messageToSocketMap.TryGetValue(messageIdentifier, out sockets))
            {
                sockets = new HashedLinkedList<SocketIdentifier>();
                messageToSocketMap[messageIdentifier] = sockets;
            }
            if (!sockets.Contains(socketIdentifier))
            {
                sockets.InsertLast(socketIdentifier);
            }
        }

        public SocketIdentifier FindRoute(Identifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(messageIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> FindAllRoutes(Identifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(messageIdentifier, out collection)
                       ? collection
                       : Enumerable.Empty<SocketIdentifier>();
        }

        private static T Get<T>(HashedLinkedList<T> hashSet)
        {
            var count = hashSet.Count;
            if (count > 0)
            {
                var first = (count > 1) ? hashSet.RemoveFirst() : hashSet.First;
                if (count > 1)
                {
                    hashSet.InsertLast(first);
                }
                return first;
            }

            return default(T);
        }

        public IEnumerable<Identifier> GetMessageIdentifiers()
            => messageToSocketMap.Keys;

        public IEnumerable<Identifier> RemoveActorHostRoute(SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<Identifier> handlers;
            if (socketToMessageMap.TryGetValue(socketIdentifier, out handlers))
            {
                foreach (var messageHandlerIdentifier in handlers)
                {
                    RemoveMessageHandler(messageHandlerIdentifier, socketIdentifier);
                    if (!messageToSocketMap.ContainsKey(messageHandlerIdentifier))
                    {
                        yield return messageHandlerIdentifier;
                    }
                }
            }
        }

        private void RemoveMessageHandler(Identifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (messageToSocketMap.TryGetValue(messageIdentifier, out hashSet))
            {
                hashSet.Remove(socketIdentifier);
                if (hashSet.Count == 0)
                {
                    messageToSocketMap.Remove(messageIdentifier);
                }
            }
        }

        public IEnumerable<InternalRoute> GetAllRoutes()
            => socketToMessageMap.Select(sm => new InternalRoute
                                               {
                                                   Socket = sm.Key,
                                                   Messages = sm.Value
                                               });

        public bool CanRouteMessage(Identifier messageIdentifier)
            => messageToSocketMap.ContainsKey(messageIdentifier);
    }
}