using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Diagnostics;
using kino.Framework;

namespace kino.Connectivity
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly C5.IDictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>> messageHandlersMap;
        private readonly C5.IDictionary<SocketIdentifier, C5.HashSet<MessageHandlerIdentifier>> socketToMessageMap;
        private readonly C5.IDictionary<SocketIdentifier, Uri> socketToUriMap;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            messageHandlersMap = new HashDictionary<MessageHandlerIdentifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new HashDictionary<SocketIdentifier, C5.HashSet<MessageHandlerIdentifier>>();
            socketToUriMap = new HashDictionary<SocketIdentifier, Uri>();
        }

        public void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier, Uri uri)
        {
            var mapped = MapMessageToSocket(messageHandlerIdentifier, socketIdentifier);

            if (mapped)
            {
                socketToUriMap[socketIdentifier] = uri;

                MapSocketToMessage(messageHandlerIdentifier, socketIdentifier);

                logger.Debug("External route added " +
                             $"Uri:{uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetString()} " +
                             $"Version:{messageHandlerIdentifier.Version.GetString()} " +
                             $"Message:{messageHandlerIdentifier.Identity.GetString()}");
            }
        }

        private bool MapMessageToSocket(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (!messageHandlersMap.Find(ref messageHandlerIdentifier, out hashSet))
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
            C5.HashSet<MessageHandlerIdentifier> hashSet;
            if (!socketToMessageMap.Find(ref socketIdentifier, out hashSet))
            {
                hashSet = new C5.HashSet<MessageHandlerIdentifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(messageHandlerIdentifier);
        }

        public SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageHandlersMap.Find(ref messageHandlerIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        public IEnumerable<SocketIdentifier> PopAll(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageHandlersMap.Find(ref messageHandlerIdentifier, out collection)
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
            Uri uri;
            socketToUriMap.Remove(socketIdentifier, out uri);

            C5.HashSet<MessageHandlerIdentifier> messageHandlers;
            if (socketToMessageMap.Find(ref socketIdentifier, out messageHandlers))
            {
                foreach (var messageHandlerIdentifier in messageHandlers)
                {
                    var _ = messageHandlerIdentifier;
                    HashedLinkedList<SocketIdentifier> socketIdentifiers;
                    if (messageHandlersMap.Find(ref _, out socketIdentifiers))
                    {
                        socketIdentifiers.Remove(socketIdentifier);
                        if (!socketIdentifiers.Any())
                        {
                            messageHandlersMap.Remove(messageHandlerIdentifier);
                        }
                    }
                }
                socketToMessageMap.Remove(socketIdentifier);

                logger.Debug($"External route removed Uri:{uri.AbsolutePath} " +
                             $"Socket:{socketIdentifier.Identity.GetString()}");
            }
        }
    }
}