using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;

namespace kino.Routing
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly C5.IDictionary<Identifier, HashedLinkedList<SocketIdentifier>> messageToSocketMap;
        private readonly C5.IDictionary<SocketIdentifier, C5.HashSet<Identifier>> socketToMessageMap;
        private readonly C5.IDictionary<SocketIdentifier, PeerConnection> socketToConnectionMap;
        private readonly C5.IDictionary<string, int> uriReferenceCount;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            messageToSocketMap = new HashDictionary<Identifier, HashedLinkedList<SocketIdentifier>>();
            socketToMessageMap = new HashDictionary<SocketIdentifier, C5.HashSet<Identifier>>();
            socketToConnectionMap = new HashDictionary<SocketIdentifier, PeerConnection>();
            uriReferenceCount = new HashDictionary<string, int>();
        }

        public PeerConnection AddMessageRoute(ExternalRouteDefinition routeDefinition)
        {
            var peerConnection = new PeerConnection
                                 {
                                     Node = routeDefinition.Peer,
                                     Health = routeDefinition.Health,
                                     Connected = false
                                 };
            var socketIdentifier = new SocketIdentifier(routeDefinition.Peer.SocketIdentity);
            socketToConnectionMap.FindOrAdd(socketIdentifier, ref peerConnection);

            if (MapMessageToSocket(routeDefinition.Identifier, socketIdentifier))
            {
                IncrementUriReferenceCount(routeDefinition.Peer.Uri.ToSocketAddress());

                MapSocketToMessage(routeDefinition.Identifier, socketIdentifier);

                logger.Debug("External route added " +
                             $"Uri:{routeDefinition.Peer.Uri.AbsoluteUri} " +
                             $"Socket:{routeDefinition.Peer.SocketIdentity.GetAnyString()} " +
                             $"Message:{routeDefinition.Identifier}");
            }

            return peerConnection;
        }

        private bool MapMessageToSocket(Identifier identifier, SocketIdentifier socketIdentifier)
        {
            HashedLinkedList<SocketIdentifier> hashSet;
            if (!messageToSocketMap.Find(ref identifier, out hashSet))
            {
                hashSet = new HashedLinkedList<SocketIdentifier>();
                messageToSocketMap[identifier] = hashSet;
            }
            if (!hashSet.Contains(socketIdentifier))
            {
                hashSet.InsertLast(socketIdentifier);
                return true;
            }

            return false;
        }

        private void MapSocketToMessage(Identifier identifier, SocketIdentifier socketIdentifier)
        {
            C5.HashSet<Identifier> hashSet;
            if (!socketToMessageMap.Find(ref socketIdentifier, out hashSet))
            {
                hashSet = new C5.HashSet<Identifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(identifier);
        }

        public PeerConnection FindRoute(Identifier identifier, byte[] receiverNodeIdentity)
        {
            HashedLinkedList<SocketIdentifier> collection;
            if (messageToSocketMap.Find(ref identifier, out collection))
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

        public IEnumerable<PeerConnection> FindAllRoutes(Identifier identifier)
        {
            HashedLinkedList<SocketIdentifier> collection;
            return messageToSocketMap.Find(ref identifier, out collection)
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

        public PeerRemoveResult RemoveNodeRoute(SocketIdentifier socketIdentifier)
        {
            PeerConnection connection;
            socketToConnectionMap.Remove(socketIdentifier, out connection);

            C5.HashSet<Identifier> identifiers;
            if (socketToMessageMap.Find(ref socketIdentifier, out identifiers))
            {
                RemoveMessageRoutesForSocketIdentifier(socketIdentifier, identifiers);

                socketToMessageMap.Remove(socketIdentifier);

                logger.Debug($"External route removed Uri:{connection.Node.Uri.AbsoluteUri} " +
                             $"Socket:{socketIdentifier.Identity.GetAnyString()}");
            }

            return new PeerRemoveResult
                   {
                       Uri = connection?.Node.Uri,
                       ConnectionAction = DecrementConnectionRefCount(connection)
                   };
        }

        private PeerConnectionAction DecrementConnectionRefCount(PeerConnection connection)
        {
            if (connection != null)
            {
                var connectionsLeft = DecrementUriReferenceCount(connection.Node.Uri.ToSocketAddress());
                return connectionsLeft == 0
                           ? PeerConnectionAction.Disconnect
                           : PeerConnectionAction.KeepConnection;
            }

            return PeerConnectionAction.NotFound;
        }

        public PeerRemoveResult RemoveMessageRoute(IEnumerable<Identifier> identifiers, SocketIdentifier socketIdentifier)
        {
            PeerConnection connection = null;

            RemoveMessageRoutesForSocketIdentifier(socketIdentifier, identifiers);

            C5.HashSet<Identifier> allSocketMessageIdentifiers;
            if (socketToMessageMap.Find(ref socketIdentifier, out allSocketMessageIdentifiers))
            {
                foreach (var messageIdentifier in identifiers)
                {
                    allSocketMessageIdentifiers.Remove(messageIdentifier);
                }
                if (!allSocketMessageIdentifiers.Any())
                {
                    socketToMessageMap.Remove(socketIdentifier);

                    socketToConnectionMap.Remove(socketIdentifier, out connection);

                    logger.Debug($"External route removed Uri:{connection?.Node.Uri.AbsoluteUri} " +
                                 $"Socket:{socketIdentifier.Identity.GetAnyString()}");
                }
            }

            logger.Debug("External message route removed " +
                         $"Socket:{socketIdentifier.Identity.GetAnyString()} " +
                         $"Messages:[{string.Join(";", ConcatenateMessageHandlers(identifiers))}]");

            return new PeerRemoveResult
                   {
                       ConnectionAction = DecrementConnectionRefCount(connection),
                       Uri = connection?.Node.Uri
                   };
        }

        private void RemoveMessageRoutesForSocketIdentifier(SocketIdentifier socketIdentifier, IEnumerable<Identifier> messageIdentifiers)
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

        private static IEnumerable<string> ConcatenateMessageHandlers(IEnumerable<Identifier> messageHandlerIdentifiers)
            => messageHandlerIdentifiers.Select(mh => mh.ToString());

        public IEnumerable<ExternalRoute> GetAllRoutes()
            => socketToMessageMap.Select(CreateExternalRoute);

        private ExternalRoute CreateExternalRoute(C5.KeyValuePair<SocketIdentifier, C5.HashSet<Identifier>> socketMessagePair)
        {
            var connection = socketToConnectionMap[socketMessagePair.Key];

            return new ExternalRoute
                   {
                       Connection = new PeerConnection
                                    {
                                        Node = connection.Node,
                                        Health = connection.Health,
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