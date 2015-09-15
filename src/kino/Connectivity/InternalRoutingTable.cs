using System.Collections.Generic;
using System.Linq;
using C5;

namespace kino.Connectivity
{
    //TODO: Add TTL for registrations, so that never consumed handlers are not staying forever

    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly System.Collections.Generic.IDictionary<SocketIdentifier, HashedLinkedList<MessageHandlerIdentifier>> socketToMessageMap;

        public InternalRoutingTable()
        {
            messageToSocketMap = new Dictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new Dictionary<SocketIdentifier, HashedLinkedList<MessageHandlerIdentifier>>();
        }

        public void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            MapHandlerToSocket(messageHandlerIdentifier, socketIdentifier);
            MapSocketToHandler(messageHandlerIdentifier, socketIdentifier);
        }

        private void MapSocketToHandler(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<MessageHandlerIdentifier> handlers;
            if (!socketToMessageMap.TryGetValue(socketIdentifier, out handlers))
            {
                handlers = new HashedLinkedList<MessageHandlerIdentifier>();
                socketToMessageMap[socketIdentifier] = handlers;
            }
            if (!handlers.Contains(messageHandlerIdentifier))
            {
                handlers.InsertLast(messageHandlerIdentifier);
            }
        }

        private void MapHandlerToSocket(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> sockets;
            if (!messageToSocketMap.TryGetValue(messageHandlerIdentifier, out sockets))
            {
                sockets = new HashedLinkedList<SocketIdentifier>();
                messageToSocketMap[messageHandlerIdentifier] = sockets;
            }
            if (!sockets.Contains(socketIdentifier))
            {
                sockets.InsertLast(socketIdentifier);
            }
        }

        public SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(messageHandlerIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> PopAll(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(messageHandlerIdentifier, out collection)
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
            => messageToSocketMap.Keys;

        public IEnumerable<MessageHandlerIdentifier> Remove(SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<MessageHandlerIdentifier> handlers;
            if (socketToMessageMap.TryGetValue(socketIdentifier, out handlers))
            {
                foreach (var messageHandlerIdentifier in handlers)
                {
                    RemoveMessageHandler(messageHandlerIdentifier, socketIdentifier);
                    if(!messageToSocketMap.ContainsKey(messageHandlerIdentifier))
                    {
                        yield return messageHandlerIdentifier;
                    }
                }
            }
        }

        private void RemoveMessageHandler(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (messageToSocketMap.TryGetValue(messageHandlerIdentifier, out hashSet))
            {
                hashSet.Remove(socketIdentifier);
                if (hashSet.Count == 0)
                {
                    messageToSocketMap.Remove(messageHandlerIdentifier);
                }
            }
        }
    }
}