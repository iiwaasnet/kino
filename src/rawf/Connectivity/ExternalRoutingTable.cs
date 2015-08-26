using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using rawf.Diagnostics;
using rawf.Framework;

namespace rawf.Connectivity
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly System.Collections.Generic.IDictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>> messageHandlersMap;
        private readonly System.Collections.Generic.IDictionary<SocketIdentifier, System.Collections.Generic.HashSet<MessageHandlerIdentifier>> socketToMessageMap;
        private readonly System.Collections.Generic.IDictionary<SocketIdentifier, Uri> socketToUriMap;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            messageHandlersMap = new Dictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new Dictionary<SocketIdentifier, System.Collections.Generic.HashSet<MessageHandlerIdentifier>>();
            socketToUriMap = new Dictionary<SocketIdentifier, Uri>();
        }

        public void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier, Uri uri)
        {
            var mapped = MapMessageToSocket(messageHandlerIdentifier, socketIdentifier);

            if (mapped)
            {
                socketToUriMap[socketIdentifier] = uri;

                MapSocketToMessage(messageHandlerIdentifier, socketIdentifier);

                logger.Debug($"Route added URI:{uri.AbsoluteUri} SOCKID:{socketIdentifier.Identity.GetString()}");
            }
        }

        private bool MapMessageToSocket(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (!messageHandlersMap.TryGetValue(messageHandlerIdentifier, out hashSet))
            {
                hashSet = new HashedLinkedList<SocketIdentifier>();
                messageHandlersMap[messageHandlerIdentifier] = hashSet;
            }
            if (!hashSet.Contains(socketIdentifier))
            {
                hashSet.InsertLast(socketIdentifier);
                return true;
            }

            return false;
        }

        private void MapSocketToMessage(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            System.Collections.Generic.HashSet<MessageHandlerIdentifier> hashSet;
            if (!socketToMessageMap.TryGetValue(socketIdentifier, out hashSet))
            {
                hashSet = new System.Collections.Generic.HashSet<MessageHandlerIdentifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(messageHandlerIdentifier);
        }

        public SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageHandlersMap.TryGetValue(messageHandlerIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> PopAll(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageHandlersMap.TryGetValue(messageHandlerIdentifier, out collection)
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

        public void RemoveRoute(SocketIdentifier socketIdentifier)
        {
            socketToUriMap.Remove(socketIdentifier);

            System.Collections.Generic.HashSet<MessageHandlerIdentifier> messageHandlers;
            if (socketToMessageMap.TryGetValue(socketIdentifier, out messageHandlers))
            {
                foreach (var messageHandlerIdentifier in messageHandlers)
                {
                    HashedLinkedList<SocketIdentifier> socketIdentifiers;
                    if (messageHandlersMap.TryGetValue(messageHandlerIdentifier, out socketIdentifiers))
                    {
                        socketIdentifiers.Remove(socketIdentifier);
                        if (!socketIdentifiers.Any())
                        {
                            messageHandlersMap.Remove(messageHandlerIdentifier);
                        }
                    }
                }
                socketToMessageMap.Remove(socketIdentifier);
            }
        }
    }
}