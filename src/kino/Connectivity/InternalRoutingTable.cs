using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using C5;
using kino.Messaging;

namespace kino.Connectivity
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever

    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<IMessageIdentifier, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly System.Collections.Generic.IDictionary<SocketIdentifier, HashedLinkedList<IMessageIdentifier>> socketToMessageMap;

        public InternalRoutingTable()
        {
            messageToSocketMap = new Dictionary<IMessageIdentifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new Dictionary<SocketIdentifier, HashedLinkedList<IMessageIdentifier>>();
        }

        public void AddMessageRoute(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            MapHandlerToSocket(messageIdentifier, socketIdentifier);
            MapSocketToHandler(messageIdentifier, socketIdentifier);
        }

        private void MapSocketToHandler(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<IMessageIdentifier> handlers;
            if (!socketToMessageMap.TryGetValue(socketIdentifier, out handlers))
            {
                handlers = new HashedLinkedList<IMessageIdentifier>();
                socketToMessageMap[socketIdentifier] = handlers;
            }
            if (!handlers.Contains(messageIdentifier))
            {
                handlers.InsertLast(messageIdentifier);
            }
        }

        private void MapHandlerToSocket(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
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

        public SocketIdentifier FindRoute(IMessageIdentifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(messageIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> FindAllRoutes(IMessageIdentifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(messageIdentifier, out collection)
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

        public IEnumerable<IMessageIdentifier> GetMessageIdentifiers()
            => messageToSocketMap.Keys;

        public IEnumerable<IMessageIdentifier> RemoveActorHostRoute(SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<IMessageIdentifier> handlers;
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

        private void RemoveMessageHandler(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
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

        public bool CanRouteMessage(IMessageIdentifier messageIdentifier)
            => messageToSocketMap.ContainsKey(messageIdentifier);
    }
}