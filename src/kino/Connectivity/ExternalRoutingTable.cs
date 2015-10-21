using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;

namespace kino.Connectivity
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly C5.IDictionary<IMessageIdentifier, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly C5.IDictionary<SocketIdentifier, C5.HashSet<IMessageIdentifier>> socketToMessageMap;
        private readonly C5.IDictionary<SocketIdentifier, Uri> socketToUriMap;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            messageToSocketMap = new HashDictionary<IMessageIdentifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new HashDictionary<SocketIdentifier, C5.HashSet<IMessageIdentifier>>();
            socketToUriMap = new HashDictionary<SocketIdentifier, Uri>();
        }

        public void AddMessageRoute(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier, Uri uri)
        {
            var mapped = MapMessageToSocket(messageIdentifier, socketIdentifier);

            if (mapped)
            {
                socketToUriMap[socketIdentifier] = uri;

                MapSocketToMessage(messageIdentifier, socketIdentifier);

                logger.Debug("External route added " +
                             $"Uri:{uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetString()} " +
                             $"Version:{messageIdentifier.Version.GetString()} " +
                             $"Message:{messageIdentifier.Identity.GetString()}");
            }
        }

        private bool MapMessageToSocket(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (!messageToSocketMap.Find(ref messageIdentifier, out hashSet))
            {
                hashSet = new HashedLinkedList<SocketIdentifier>();
                messageToSocketMap[messageIdentifier] = hashSet;
            }
            if (!hashSet.Contains(socketIdentifier))
            {
                hashSet.InsertLast(socketIdentifier);
                return true;
            }

            return false;
        }

        private void MapSocketToMessage(IMessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            C5.HashSet<IMessageIdentifier> hashSet;
            if (!socketToMessageMap.Find(ref socketIdentifier, out hashSet))
            {
                hashSet = new C5.HashSet<IMessageIdentifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(messageIdentifier);
        }

        public Node FindRoute(IMessageIdentifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            if (messageToSocketMap.Find(ref messageIdentifier, out collection))
            {
                var socketIdentifier = Get(collection);
                return new Node(socketToUriMap[socketIdentifier], socketIdentifier.Identity);
            }

            return null;
        }

        public IEnumerable<Node> FindAllRoutes(IMessageIdentifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.Find(ref messageIdentifier, out collection)
                       ? collection.Select(el => new Node(socketToUriMap[el], el.Identity))
                       : Enumerable.Empty<Node>();
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

        public void RemoveNodeRoute(SocketIdentifier socketIdentifier)
        {
            Uri uri;
            socketToUriMap.Remove(socketIdentifier, out uri);

            C5.HashSet<IMessageIdentifier> messageIdentifiers;
            if (socketToMessageMap.Find(ref socketIdentifier, out messageIdentifiers))
            {
                RemoveMessageRoutesForSocketIdentifier(socketIdentifier, messageIdentifiers);

                socketToMessageMap.Remove(socketIdentifier);

                logger.Debug($"External route removed Uri:{uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetString()}");
            }
        }

        

        public void RemoveMessageRoute(IEnumerable<IMessageIdentifier> messageIdentifiers, SocketIdentifier socketIdentifier)
        {
            RemoveMessageRoutesForSocketIdentifier(socketIdentifier, messageIdentifiers);
            
            C5.HashSet<IMessageIdentifier> allSocketMessageIdentifiers;
            if (socketToMessageMap.Find(ref socketIdentifier, out allSocketMessageIdentifiers))
            {
                foreach (var messageIdentifier in messageIdentifiers)
                {
                    allSocketMessageIdentifiers.Remove(messageIdentifier);
                }
                if (!allSocketMessageIdentifiers.Any())
                {
                    socketToMessageMap.Remove(socketIdentifier);
                    Uri uri;
                    socketToUriMap.Remove(socketIdentifier, out uri);

                    logger.Debug($"External route removed Uri:{uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetString()}");
                }
            }

            logger.Debug($"External message route removed " +
                                 $"Socket:{socketIdentifier.Identity.GetString()} " +
                                 $"Messages:[{string.Join(";", ConcatenateMessageHandlers(messageIdentifiers))}]");

        }

        private void RemoveMessageRoutesForSocketIdentifier(SocketIdentifier socketIdentifier, IEnumerable<IMessageIdentifier> messageIdentifiers)
        {
            foreach (var messageIdentifier in messageIdentifiers)
            {
                var tmpMessageIdentifier = messageIdentifier;
                HashedLinkedList<SocketIdentifier> socketIdentifiers;
                if (messageToSocketMap.Find(ref tmpMessageIdentifier, out socketIdentifiers))
                {
                    socketIdentifiers.Remove(socketIdentifier);
                    if (!socketIdentifiers.Any())
                    {
                        messageToSocketMap.Remove(messageIdentifier);
                    }
                }
            }
        }

        private static IEnumerable<string> ConcatenateMessageHandlers(IEnumerable<IMessageIdentifier> messageHandlerIdentifiers)
            => messageHandlerIdentifiers.Select(mh => $"{mh.Identity.GetString()}:{mh.Version.GetString()}");

        public IEnumerable<ExternalRoute> GetAllRoutes()
            => socketToMessageMap.Select(sm => new ExternalRoute
                                               {
                                                   Node = new Node(socketToUriMap[sm.Key], sm.Key.Identity),
                                                   Messages = sm.Value
                                               });
    }
}