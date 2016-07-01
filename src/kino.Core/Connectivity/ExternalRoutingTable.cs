using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core.Diagnostics;
using kino.Core.Framework;

namespace kino.Core.Connectivity
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly C5.IDictionary<MessageIdentifier, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly C5.IDictionary<SocketIdentifier, C5.HashSet<MessageIdentifier>> socketToMessageMap;
        private readonly C5.IDictionary<SocketIdentifier, PeerConnection> socketToConnectionMap;
        private readonly C5.IDictionary<string, int> uriReferenceCount;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            messageToSocketMap = new HashDictionary<MessageIdentifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new HashDictionary<SocketIdentifier, C5.HashSet<MessageIdentifier>>();
            socketToConnectionMap = new HashDictionary<SocketIdentifier, PeerConnection>();
            uriReferenceCount = new HashDictionary<string, int>();
        }

        public PeerConnection AddMessageRoute(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier, Uri uri)
        {
            var peerConnection = new PeerConnection
                                 {
                                     Node = new Node(uri, socketIdentifier.Identity),
                                     Connected = false
                                 };
            var socketAlreadyFound = socketToConnectionMap.FindOrAdd(socketIdentifier, ref peerConnection);

            if (MapMessageToSocket(messageIdentifier, socketIdentifier))
            {
                if (!socketAlreadyFound)
                {
                    IncrementUriReferenceCount(uri.ToSocketAddress());
                }

                MapSocketToMessage(messageIdentifier, socketIdentifier);

                logger.Debug("External route added " +
                             $"Uri:{uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetAnyString()} " +
                             $"Message:{messageIdentifier}");
            }

            return peerConnection;
        }

        private bool MapMessageToSocket(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
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

        private void MapSocketToMessage(MessageIdentifier messageIdentifier, SocketIdentifier socketIdentifier)
        {
            C5.HashSet<MessageIdentifier> hashSet;
            if (!socketToMessageMap.Find(ref socketIdentifier, out hashSet))
            {
                hashSet = new C5.HashSet<MessageIdentifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(messageIdentifier);
        }

        public PeerConnection FindRoute(MessageIdentifier messageIdentifier, byte[] receiverNodeIdentity)
        {
            HashedLinkedList<SocketIdentifier> collection;
            if (messageToSocketMap.Find(ref messageIdentifier, out collection))
            {
                var socketIdentifier = GetReceiverSocketIdentifier(collection, receiverNodeIdentity);
                PeerConnection peerConnection;
                if (socketToConnectionMap.Find(ref socketIdentifier, out peerConnection))
                {
                    return peerConnection;
                }
            }

            return null;
        }

        private SocketIdentifier GetReceiverSocketIdentifier(HashedLinkedList<SocketIdentifier> collection, byte[] receiverNodeIdentity)
        {
            if (receiverNodeIdentity.IsSet())
            {
                var socketIdentifier = new SocketIdentifier(receiverNodeIdentity);
                return collection.Find(ref socketIdentifier)
                           ? socketIdentifier
                           : null;
            }
            return Get(collection);
        }

        public IEnumerable<PeerConnection> FindAllRoutes(MessageIdentifier messageIdentifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.Find(ref messageIdentifier, out collection)
                       ? collection.Select(el => socketToConnectionMap[el])
                       : Enumerable.Empty<PeerConnection>();
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

        public PeerConnectionAction RemoveNodeRoute(SocketIdentifier socketIdentifier)
        {
            PeerConnection connection;
            socketToConnectionMap.Remove(socketIdentifier, out connection);

            C5.HashSet<MessageIdentifier> messageIdentifiers;
            if (socketToMessageMap.Find(ref socketIdentifier, out messageIdentifiers))
            {
                RemoveMessageRoutesForSocketIdentifier(socketIdentifier, messageIdentifiers);

                socketToMessageMap.Remove(socketIdentifier);

                logger.Debug($"External route removed Uri:{connection.Node.Uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetAnyString()}");
            }

            // NOTE: Don't change order so that ref count is decreased
            return (connection != null
                    && DecrementUriReferenceCount(connection.Node.Uri.ToSocketAddress()) == 0
                    && connection.Connected)
                       ? PeerConnectionAction.Disconnect
                       : PeerConnectionAction.None;
        }

        public PeerConnectionAction RemoveMessageRoute(IEnumerable<MessageIdentifier> messageIdentifiers, SocketIdentifier socketIdentifier)
        {
            var connectionAction = PeerConnectionAction.None;

            RemoveMessageRoutesForSocketIdentifier(socketIdentifier, messageIdentifiers);

            C5.HashSet<MessageIdentifier> allSocketMessageIdentifiers;
            if (socketToMessageMap.Find(ref socketIdentifier, out allSocketMessageIdentifiers))
            {
                foreach (var messageIdentifier in messageIdentifiers)
                {
                    allSocketMessageIdentifiers.Remove(messageIdentifier);
                }
                if (!allSocketMessageIdentifiers.Any())
                {
                    socketToMessageMap.Remove(socketIdentifier);
                    PeerConnection connection;
                    socketToConnectionMap.Remove(socketIdentifier, out connection);
                    // NOTE: Don't change order so that ref count is decreased
                    connectionAction = (connection != null
                                        && DecrementUriReferenceCount(connection.Node.Uri.ToSocketAddress()) == 0
                                        && connection.Connected)
                                           ? PeerConnectionAction.Disconnect
                                           : connectionAction;

                    logger.Debug($"External route removed Uri:{connection?.Node.Uri.AbsoluteUri} " +
                                 $"Socket:{socketIdentifier.Identity.GetAnyString()}");
                }
            }

            logger.Debug("External message route removed " +
                         $"Socket:{socketIdentifier.Identity.GetAnyString()} " +
                         $"Messages:[{string.Join(";", ConcatenateMessageHandlers(messageIdentifiers))}]");

            return connectionAction;
        }

        private void RemoveMessageRoutesForSocketIdentifier(SocketIdentifier socketIdentifier, IEnumerable<MessageIdentifier> messageIdentifiers)
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

        private static IEnumerable<string> ConcatenateMessageHandlers(IEnumerable<MessageIdentifier> messageHandlerIdentifiers)
            => messageHandlerIdentifiers.Select(mh => mh.ToString());

        public IEnumerable<ExternalRoute> GetAllRoutes()
            => socketToMessageMap.Select(CreateExternalRoute);

        private ExternalRoute CreateExternalRoute(C5.KeyValuePair<SocketIdentifier, C5.HashSet<MessageIdentifier>> socketMessagePair)
        {
            var connection = socketToConnectionMap[socketMessagePair.Key];

            return new ExternalRoute
                   {
                       Connection = new PeerConnection
                                    {
                                        Node = connection.Node,
                                        Connected = connection.Connected
                                    },
                       Messages = socketMessagePair.Value
                   };
        }

        private int IncrementUriReferenceCount(string uri)
        {
            var refCount = 0;
            uriReferenceCount.FindOrAdd(uri, ref refCount);
            refCount++;
            uriReferenceCount[uri] = refCount;

            logger.Debug($"New connection to {uri}. Total count: {refCount}");

            return refCount;
        }

        private int DecrementUriReferenceCount(string uri)
        {
            var refCount = 0;
            if (uriReferenceCount.Find(ref uri, out refCount))
            {
                refCount--;
                if (refCount <= 0)
                {
                    uriReferenceCount.Remove(uri);
                }
                else
                {
                    uriReferenceCount[uri] = refCount;
                }
            }

            logger.Debug($"Removed connection to {uri}. Connections left: {refCount}");

            return (refCount < 0) ? 0 : refCount;
        }
    }
}