using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;

namespace kino.Routing
{
    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<IdentityRegistration, HashedLinkedList<ILocalSendingSocket<IMessage>>> messageToSocketMap;
        private readonly System.Collections.Generic.IDictionary<ILocalSendingSocket<IMessage>, HashedLinkedList<IdentityRegistration>> socketToMessageMap;

        public InternalRoutingTable()
        {
            messageToSocketMap = new Dictionary<IdentityRegistration, HashedLinkedList<ILocalSendingSocket<IMessage>>>();
            socketToMessageMap = new Dictionary<ILocalSendingSocket<IMessage>, HashedLinkedList<IdentityRegistration>>();
        }

        public void AddMessageRoute(IdentityRegistration identityRegistration, ILocalSendingSocket<IMessage> receivingSocket)
        {
            MapHandlerToSocket(identityRegistration, receivingSocket);
            MapSocketToHandler(identityRegistration, receivingSocket);
        }

        private void MapSocketToHandler(IdentityRegistration messageIdentifier, ILocalSendingSocket<IMessage> receivingSocket)
        {
            HashedLinkedList<IdentityRegistration> handlers;
            if (!socketToMessageMap.TryGetValue(receivingSocket, out handlers))
            {
                handlers = new HashedLinkedList<IdentityRegistration>();
                socketToMessageMap[receivingSocket] = handlers;
            }
            if (!handlers.Contains(messageIdentifier))
            {
                handlers.InsertLast(messageIdentifier);
            }
        }

        private void MapHandlerToSocket(IdentityRegistration messageIdentifier, ILocalSendingSocket<IMessage> receivingSocket)
        {
            HashedLinkedList<ILocalSendingSocket<IMessage>> sockets;
            if (!messageToSocketMap.TryGetValue(messageIdentifier, out sockets))
            {
                sockets = new HashedLinkedList<ILocalSendingSocket<IMessage>>();
                messageToSocketMap[messageIdentifier] = sockets;
            }
            if (!sockets.Contains(receivingSocket))
            {
                sockets.InsertLast(receivingSocket);
            }
        }

        public ILocalSendingSocket<IMessage> FindRoute(Identifier identifier)
        {
            HashedLinkedList<ILocalSendingSocket<IMessage>> collection;
            return messageToSocketMap.TryGetValue(new IdentityRegistration(identifier), out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<ILocalSendingSocket<IMessage>> FindAllRoutes(Identifier identifier)
        {
            HashedLinkedList<ILocalSendingSocket<IMessage>> collection;
            return messageToSocketMap.TryGetValue(new IdentityRegistration(identifier), out collection)
                       ? collection
                       : Enumerable.Empty<ILocalSendingSocket<IMessage>>();
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

        public IEnumerable<IdentityRegistration> RemoveActorHostRoute(ILocalSendingSocket<IMessage> receivingSocket)
        {
            HashedLinkedList<IdentityRegistration> handlers;
            if (socketToMessageMap.TryGetValue(receivingSocket, out handlers))
            {
                foreach (var messageHandlerIdentifier in handlers)
                {
                    RemoveMessageHandler(messageHandlerIdentifier, receivingSocket);
                    if (!messageToSocketMap.ContainsKey(messageHandlerIdentifier))
                    {
                        yield return messageHandlerIdentifier;
                    }
                }
            }
        }

        private void RemoveMessageHandler(IdentityRegistration messageIdentifier, ILocalSendingSocket<IMessage> receivingSocket)
        {
            HashedLinkedList<ILocalSendingSocket<IMessage>> hashSet;
            if (messageToSocketMap.TryGetValue(messageIdentifier, out hashSet))
            {
                hashSet.Remove(receivingSocket);
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