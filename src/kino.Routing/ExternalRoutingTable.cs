using System;
using System.Linq;
using C5;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using Bcl = System.Collections.Generic;

namespace kino.Routing
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>> nodeMessageHubs;
        private readonly Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>> nodeActors;
        private readonly Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>> actorToMessageMap;
        private readonly Bcl.IDictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>> messageToNodeMap;
        private readonly Bcl.IDictionary<ReceiverIdentifier, PeerConnection> nodeToConnectionMap;
        private readonly Bcl.IDictionary<string, int> uriReferenceCount;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            nodeMessageHubs = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
            nodeActors = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
            actorToMessageMap = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>>();
            messageToNodeMap = new Bcl.Dictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>>();
            nodeToConnectionMap = new Bcl.Dictionary<ReceiverIdentifier, PeerConnection>();
            uriReferenceCount = new Bcl.Dictionary<string, int>();
        }

        public PeerConnection AddMessageRoute(ExternalRouteRegistration routeRegistration)
        {
            var receiverNode = new ReceiverIdentifier(routeRegistration.Peer.SocketIdentity);
            Func<bool> receiverToNodeMap = () => false;

            if (routeRegistration.Route.Receiver.IsActor())
            {
                MapMessageToNode(routeRegistration, receiverNode);
                MapActorToMessage(routeRegistration);
                receiverToNodeMap = () => MapActorToNode(routeRegistration, receiverNode);
            }
            else
            {
                if (routeRegistration.Route.Receiver.IsMessageHub())
                {
                    receiverToNodeMap = () => MapMessageHubToNode(routeRegistration, receiverNode);
                }
                else
                {
                    throw new ArgumentException($"Requested registration is for unknown Receiver type: [{routeRegistration.Route.Receiver}]!");
                }
            }
            if (receiverToNodeMap())
            {
                IncrementUriReferenceCount(routeRegistration.Peer.Uri.ToSocketAddress());
            }

            return MapNodeToConnection(routeRegistration, receiverNode);
        }

        private PeerConnection MapNodeToConnection(ExternalRouteRegistration routeRegistration, ReceiverIdentifier receiverNode)
        {
            var peerConnection = default(PeerConnection);
            if (!nodeToConnectionMap.TryGetValue(receiverNode, out peerConnection))
            {
                peerConnection = new PeerConnection
                                 {
                                     Node = routeRegistration.Peer,
                                     Health = routeRegistration.Health,
                                     Connected = false
                                 };
                nodeToConnectionMap[receiverNode] = peerConnection;
            }
            return peerConnection;
        }

        private bool MapMessageHubToNode(ExternalRouteRegistration routeRegistration, ReceiverIdentifier receiverNode)
        {
            var messageHub = routeRegistration.Route.Receiver;
            Bcl.HashSet<ReceiverIdentifier> messageHubs;
            if (!nodeMessageHubs.TryGetValue(receiverNode, out messageHubs))
            {
                messageHubs = new Bcl.HashSet<ReceiverIdentifier>();
                nodeMessageHubs[receiverNode] = messageHubs;
            }
            return messageHubs.Add(messageHub);
        }

        private void MapActorToMessage(ExternalRouteRegistration routeRegistration)
        {
            Bcl.HashSet<MessageIdentifier> actorMessages;
            if (!actorToMessageMap.TryGetValue(routeRegistration.Route.Receiver, out actorMessages))
            {
                actorMessages = new Bcl.HashSet<MessageIdentifier>();
                actorToMessageMap[routeRegistration.Route.Receiver] = actorMessages;
            }
            actorMessages.Add(routeRegistration.Route.Message);
        }

        private bool MapActorToNode(ExternalRouteRegistration routeRegistration, ReceiverIdentifier receiverNode)
        {
            Bcl.HashSet<ReceiverIdentifier> actors;
            if (!nodeActors.TryGetValue(receiverNode, out actors))
            {
                actors = new Bcl.HashSet<ReceiverIdentifier>();
                nodeActors[receiverNode] = actors;
            }
            return actors.Add(routeRegistration.Route.Receiver);
        }

        private void MapMessageToNode(ExternalRouteRegistration routeRegistration, ReceiverIdentifier receiverNode)
        {
            var messageIdentifier = routeRegistration.Route.Message;
            HashedLinkedList<ReceiverIdentifier> nodes;
            if (!messageToNodeMap.TryGetValue(messageIdentifier, out nodes))
            {
                nodes = new HashedLinkedList<ReceiverIdentifier>();
                messageToNodeMap[messageIdentifier] = nodes;
            }
            if (!nodes.Contains(receiverNode))
            {
                nodes.InsertLast(receiverNode);
            }
        }

        public Bcl.IEnumerable<PeerConnection> FindRoutes(ExternalRouteLookupRequest lookupRequest)
        {
            var peers = new Bcl.List<PeerConnection>();
            PeerConnection peerConnection;
            if (lookupRequest.ReceiverNodeIdentity.IsSet() && nodeToConnectionMap.TryGetValue(lookupRequest.ReceiverNodeIdentity, out peerConnection))
            {
                peers.Add(peerConnection);
            }
            else
            {
                HashedLinkedList<ReceiverIdentifier> nodes;
                if (messageToNodeMap.TryGetValue(lookupRequest.Message, out nodes))
                {
                    if (lookupRequest.Distribution == DistributionPattern.Unicast)
                    {
                        peers.Add(nodeToConnectionMap[Get(nodes)]);
                    }
                    else
                    {
                        if (lookupRequest.Distribution == DistributionPattern.Broadcast)
                        {
                            foreach (var node in nodes)
                            {
                                peers.Add(nodeToConnectionMap[node]);
                            }
                        }
                    }
                }
            }

            return peers;
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
            nodeToConnectionMap.Remove(socketIdentifier, out connection);

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

        public PeerRemoveResult RemoveMessageRoute(ExternalRouteRemoval routeRemoval)
        {
            PeerConnection connection = null;
            var connectionAction = PeerConnectionAction.NotFound;

            var receiverNode = new ReceiverIdentifier(routeRemoval.Peer.SocketIdentity);
            if (nodeToConnectionMap.TryGetValue(receiverNode, out connection))
            {
                if (routeRemoval.Route.Receiver.IsMessageHub())
                {
                    Bcl.HashSet<ReceiverIdentifier> messageHubs;
                    if (nodeMessageHubs.TryGetValue(receiverNode, out messageHubs))
                    {
                        if (messageHubs.Remove(routeRemoval.Route.Receiver))
                        {
                            connectionAction = DecrementConnectionRefCount(connection);
                            if (!messageHubs.Any())
                            {
                                nodeMessageHubs.Remove(receiverNode);
                            }
                            logger.Debug("External Actor removed " +
                                         $"Socket:{receiverNode} " +
                                         $"Identity:[{routeRemoval.Route.Receiver}]");
                        }
                    }
                }
                else
                {
                    if (routeRemoval.Route.Receiver.IsActor())
                    {
                        Bcl.HashSet<MessageIdentifier> messages;
                        if (actorToMessageMap.TryGetValue(routeRemoval.Route.Receiver, out messages))
                        {
                            messages.Remove(routeRemoval.Route.Message);
                            if (!messages.Any())
                            {
                                actorToMessageMap.Remove(routeRemoval.Route.Receiver);
                                //
                                Bcl.HashSet<ReceiverIdentifier> actors;
                                if (nodeActors.TryGetValue(receiverNode, out actors))
                                {
                                    if (actors.Remove(routeRemoval.Route.Receiver))
                                    {
                                        connectionAction = DecrementConnectionRefCount(connection);
                                        if (!actors.Any())
                                        {
                                            nodeActors.Remove(receiverNode);
                                        }
                                        //
                                        HashedLinkedList<ReceiverIdentifier> nodes;
                                        if (messageToNodeMap.TryGetValue(routeRemoval.Route.Message, out nodes))
                                        {
                                            nodes.Remove(receiverNode);
                                            if (!nodes.Any())
                                            {
                                                messageToNodeMap.Remove(routeRemoval.Route.Message);
                                            }
                                        }
                                    }
                                }
                            }
                            logger.Debug("External message route removed " +
                                         $"Socket:{receiverNode} " +
                                         $"Message:[{routeRemoval.Route.Message}]");
                        }
                    }
                }
                if (!nodeActors.ContainsKey(receiverNode) && !nodeMessageHubs.ContainsKey(receiverNode))
                {
                    nodeToConnectionMap.Remove(receiverNode);
                    logger.Debug($"External route removed Uri:{connection?.Node.Uri.AbsoluteUri} " +
                                 $"Socket:{receiverNode}");
                }
            }

            return new PeerRemoveResult
                   {
                       ConnectionAction = connectionAction,
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

        //public Bcl.IEnumerable<ExternalRoute> GetAllRoutes()
        //    => socketToMessageMap.Select(CreateExternalRoute);

        //private ExternalRoute CreateExternalRoute(KeyValuePair<ReceiverIdentifier, HashSet<Identifier>> socketMessagePair)
        //{
        //    var connection = nodeToConnectionMap[socketMessagePair.Key];

        //    return new ExternalRoute
        //           {
        //               Connection = new PeerConnection
        //                            {
        //                                Node = connection.Node,
        //                                Health = connection.Health,
        //                                Connected = connection.Connected
        //                            },
        //               Messages = socketMessagePair.Value
        //           };
        //}

        private int IncrementUriReferenceCount(string uri)
        {
            var refCount = 0;
            uriReferenceCount.TryGetValue(uri, out refCount);
            uriReferenceCount[uri] = ++refCount;

            logger.Debug($"New connection to {uri}. Total count: {refCount}");

            return refCount;
        }

        private int DecrementUriReferenceCount(string uri)
        {
            var refCount = 0;
            if (uriReferenceCount.TryGetValue(uri, out refCount))
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