using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<IdentityRegistration, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly System.Collections.Generic.IDictionary<SocketIdentifier, HashedLinkedList<IdentityRegistration>> socketToMessageMap;

        public InternalRoutingTable()
        {
            messageToSocketMap = new Dictionary<IdentityRegistration, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new Dictionary<SocketIdentifier, HashedLinkedList<IdentityRegistration>>();
        }

        public void AddMessageRoute(IdentityRegistration identityRegistration, SocketIdentifier socketIdentifier)
        {
            MapHandlerToSocket(identityRegistration, socketIdentifier);
            MapSocketToHandler(identityRegistration, socketIdentifier);
        }

        private void MapSocketToHandler(IdentityRegistration messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<IdentityRegistration> handlers;
            if (!socketToMessageMap.TryGetValue(socketIdentifier, out handlers))
            {
                handlers = new HashedLinkedList<IdentityRegistration>();
                socketToMessageMap[socketIdentifier] = handlers;
            }
            if (!handlers.Contains(messageIdentifier))
            {
                handlers.InsertLast(messageIdentifier);
            }
        }

        private void MapHandlerToSocket(IdentityRegistration messageIdentifier, SocketIdentifier socketIdentifier)
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

        public SocketIdentifier FindRoute(Identifier identifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(new IdentityRegistration(identifier), out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> FindAllRoutes(Identifier identifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.TryGetValue(new IdentityRegistration(identifier), out collection)
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

        public IEnumerable<IdentityRegistration> GetMessageRegistrations()
            => messageToSocketMap.Keys;

        public IEnumerable<IdentityRegistration> RemoveActorHostRoute(SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<IdentityRegistration> handlers;
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

        private void RemoveMessageHandler(IdentityRegistration messageIdentifier, SocketIdentifier socketIdentifier)
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
                                                                .Select(v => v.Identifier)
                                                                .ToList()
                                               });

        public bool MessageHandlerRegisteredExternaly(Identifier identifier)
            => messageToSocketMap.Keys
                                 .Any(k => !k.LocalRegistration && k == new IdentityRegistration(identifier));
    }
}