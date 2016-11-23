using System.Linq;
using C5;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using Bcl = System.Collections.Generic;

namespace kino.Routing
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly Bcl.IDictionary<ReceiverIdentifier, HashSet<ReceiverIdentifier>> messageHubs;
        private readonly Bcl.IDictionary<ReceiverIdentifier, HashSet<ReceiverIdentifier>> actors;
        private readonly Bcl.IDictionary<ReceiverIdentifier, HashSet<MessageIdentifier>> actorToMessageMap;
        private readonly Bcl.IDictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>> messageToNodeMap;
        private readonly Bcl.IDictionary<ReceiverIdentifier, PeerConnection> socketToConnectionMap;
        private readonly Bcl.IDictionary<string, int> uriReferenceCount;
        //private readonly Bcl.IDictionary<Identifier, HashedLinkedList<ReceiverIdentifier>> messageToSocketMap;
        //private readonly Bcl.IDictionary<ReceiverIdentifier, C5.HashSet<Identifier>> socketToMessageMap;

        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            messageHubs = new Bcl.Dictionary<ReceiverIdentifier, HashSet<ReceiverIdentifier>>();
            actors = new Bcl.Dictionary<ReceiverIdentifier, HashSet<ReceiverIdentifier>>();
            actorToMessageMap = new Bcl.Dictionary<ReceiverIdentifier, HashSet<MessageIdentifier>>();
            messageToNodeMap = new Bcl.Dictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>>();
            socketToConnectionMap = new Bcl.Dictionary<ReceiverIdentifier, PeerConnection>();
            uriReferenceCount = new Bcl.Dictionary<string, int>();
            //messageToSocketMap = new Dictionary<Identifier, HashedLinkedList<ReceiverIdentifier>>();
            //socketToMessageMap = new Dictionary<ReceiverIdentifier, C5.HashSet<Identifier>>();
        }

        public PeerConnection AddMessageRoute(ExternalRouteRegistration routeRegistration)
        {
            throw new System.NotImplementedException();
        }

        public PeerConnection AddMessageRoute(ExternalRouteDefinition routeDefinition)
        {
            var peerConnection = new PeerConnection
                                 {
                                     Node = routeDefinition.Peer,
                                     Health = routeDefinition.Health,
                                     Connected = false
                                 };
            var socketIdentifier = new ReceiverIdentifier(routeDefinition.Peer.SocketIdentity);
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

        private bool MapMessageToSocket(Identifier identifier, ReceiverIdentifier socketIdentifier)
        {
            HashedLinkedList<ReceiverIdentifier> hashSet;
            if (!messageToSocketMap.Find(ref identifier, out hashSet))
            {
                hashSet = new HashedLinkedList<ReceiverIdentifier>();
                messageToSocketMap[identifier] = hashSet;
            }
            if (!hashSet.Contains(socketIdentifier))
            {
                hashSet.InsertLast(socketIdentifier);
                return true;
            }

            return false;
        }

        private void MapSocketToMessage(Identifier identifier, ReceiverIdentifier socketIdentifier)
        {
            HashSet<Identifier> hashSet;
            if (!socketToMessageMap.Find(ref socketIdentifier, out hashSet))
            {
                hashSet = new HashSet<Identifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(identifier);
        }

        public PeerConnection FindRoute(Identifier identifier, byte[] receiverNodeIdentity)
        {
            HashedLinkedList<ReceiverIdentifier> collection;
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

        private ReceiverIdentifier GetReceiverSocketIdentifier(HashedLinkedList<ReceiverIdentifier> collection, byte[] receiverNodeIdentity)
        {
            if (receiverNodeIdentity.IsSet())
            {
                var socketIdentifier = new ReceiverIdentifier(receiverNodeIdentity);
                return collection.Find(ref socketIdentifier)
                           ? socketIdentifier
                           : null;
            }
            return Get(collection);
        }

        public Bcl.IEnumerable<PeerConnection> FindAllRoutes(Identifier identifier)
        {
            HashedLinkedList<ReceiverIdentifier> collection;
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

        public PeerRemoveResult RemoveNodeRoute(ReceiverIdentifier socketIdentifier)
        {
            PeerConnection connection;
            socketToConnectionMap.Remove(socketIdentifier, out connection);

            HashSet<Identifier> identifiers;
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

        public PeerRemoveResult RemoveMessageRoute(Bcl.IEnumerable<Identifier> identifiers, ReceiverIdentifier socketIdentifier)
        {
            PeerConnection connection = null;

            RemoveMessageRoutesForSocketIdentifier(socketIdentifier, identifiers);

            HashSet<Identifier> allSocketMessageIdentifiers;
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

        private void RemoveMessageRoutesForSocketIdentifier(ReceiverIdentifier socketIdentifier, Bcl.IEnumerable<Identifier> messageIdentifiers)
        {
            foreach (var messageIdentifier in messageIdentifiers)
            {
                var tmpMessageIdentifier = messageIdentifier;
                HashedLinkedList<ReceiverIdentifier> socketIdentifiers;
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

        private static Bcl.IEnumerable<string> ConcatenateMessageHandlers(Bcl.IEnumerable<Identifier> messageHandlerIdentifiers)
            => messageHandlerIdentifiers.Select(mh => mh.ToString());

        public Bcl.IEnumerable<ExternalRoute> GetAllRoutes()
            => socketToMessageMap.Select(CreateExternalRoute);

        private ExternalRoute CreateExternalRoute(KeyValuePair<ReceiverIdentifier, HashSet<Identifier>> socketMessagePair)
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