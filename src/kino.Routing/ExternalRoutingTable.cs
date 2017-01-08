using System;
using System.Collections.Concurrent;
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
        private readonly Bcl.IDictionary<string, Bcl.HashSet<ReceiverIdentifier>> uriToNodeMap;
        private readonly ILogger logger;

        public ExternalRoutingTable(ILogger logger)
        {
            this.logger = logger;
            nodeMessageHubs = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
            nodeActors = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
            actorToMessageMap = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>>();
            messageToNodeMap = new Bcl.Dictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>>();
            nodeToConnectionMap = new Bcl.Dictionary<ReceiverIdentifier, PeerConnection>();
            uriToNodeMap = new ConcurrentDictionary<string, Bcl.HashSet<ReceiverIdentifier>>();
        }

        public PeerConnection AddMessageRoute(ExternalRouteRegistration routeRegistration)
        {
            var nodeIdentifier = new ReceiverIdentifier(routeRegistration.Peer.SocketIdentity);

            if (routeRegistration.Route.Receiver.IsActor())
            {
                MapMessageToNode(routeRegistration, nodeIdentifier);
                MapActorToMessage(routeRegistration);
                MapActorToNode(routeRegistration, nodeIdentifier);

                logger.Debug("External route added " +
                             $"Uri:{routeRegistration.Peer.Uri.AbsoluteUri} " +
                             $"Node:{nodeIdentifier} " +
                             $"Actor:{routeRegistration.Route.Receiver}" +
                             $"Message:{routeRegistration.Route.Message}");
            }
            else
            {
                if (routeRegistration.Route.Receiver.IsMessageHub())
                {
                    MapMessageHubToNode(routeRegistration, nodeIdentifier);

                    logger.Debug("External route added " +
                                 $"Uri:{routeRegistration.Peer.Uri.AbsoluteUri} " +
                                 $"Node:{nodeIdentifier} " +
                                 $"MessageHub:{routeRegistration.Route.Receiver}");
                }
                else
                {
                    throw new ArgumentException($"Requested registration is for unknown Receiver type: [{routeRegistration.Route.Receiver}]!");
                }
            }

            var connection = MapNodeToConnection(routeRegistration, nodeIdentifier);
            MapConnectionToNode(connection);

            return connection;
        }

        private void MapConnectionToNode(PeerConnection connection)
        {
            Bcl.HashSet<ReceiverIdentifier> nodes;
            var uri = connection.Node.Uri.ToSocketAddress();
            if (!uriToNodeMap.TryGetValue(uri, out nodes))
            {
                nodes = new Bcl.HashSet<ReceiverIdentifier>();
                uriToNodeMap[uri] = nodes;
            }

            nodes.Add(new ReceiverIdentifier(connection.Node.SocketIdentity));

            logger.Debug($"[{nodes.Count}] node(s) registered at {uri}.");
        }

        private PeerConnection MapNodeToConnection(ExternalRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            var peerConnection = default(PeerConnection);
            if (!nodeToConnectionMap.TryGetValue(nodeIdentifier, out peerConnection))
            {
                peerConnection = new PeerConnection
                                 {
                                     Node = routeRegistration.Peer,
                                     Health = routeRegistration.Health,
                                     Connected = false
                                 };
                nodeToConnectionMap[nodeIdentifier] = peerConnection;
            }
            return peerConnection;
        }

        private void MapMessageHubToNode(ExternalRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            var messageHub = routeRegistration.Route.Receiver;
            Bcl.HashSet<ReceiverIdentifier> messageHubs;
            if (!nodeMessageHubs.TryGetValue(nodeIdentifier, out messageHubs))
            {
                messageHubs = new Bcl.HashSet<ReceiverIdentifier>();
                nodeMessageHubs[nodeIdentifier] = messageHubs;
            }
            messageHubs.Add(messageHub);
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

        private void MapActorToNode(ExternalRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            Bcl.HashSet<ReceiverIdentifier> actors;
            if (!nodeActors.TryGetValue(nodeIdentifier, out actors))
            {
                actors = new Bcl.HashSet<ReceiverIdentifier>();
                nodeActors[nodeIdentifier] = actors;
            }
            actors.Add(routeRegistration.Route.Receiver);
        }

        private void MapMessageToNode(ExternalRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            var messageIdentifier = routeRegistration.Route.Message;
            HashedLinkedList<ReceiverIdentifier> nodes;
            if (!messageToNodeMap.TryGetValue(messageIdentifier, out nodes))
            {
                nodes = new HashedLinkedList<ReceiverIdentifier>();
                messageToNodeMap[messageIdentifier] = nodes;
            }
            if (!nodes.Contains(nodeIdentifier))
            {
                nodes.InsertLast(nodeIdentifier);
            }
        }

        public Bcl.IEnumerable<PeerConnection> FindRoutes(ExternalRouteLookupRequest lookupRequest)
        {
            var peers = new Bcl.List<PeerConnection>();
            if (lookupRequest.ReceiverNodeIdentity.IsSet())
            {
                PeerConnection peerConnection;
                if (nodeToConnectionMap.TryGetValue(lookupRequest.ReceiverNodeIdentity, out peerConnection))
                {
                    peers.Add(peerConnection);
                }
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

        private static T Get<T>(IList<T> hashSet)
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

        public PeerRemoveResult RemoveNodeRoute(ReceiverIdentifier nodeIdentifier)
        {
            PeerConnection connection;
            var peerConnectionAction = PeerConnectionAction.NotFound;

            if (nodeToConnectionMap.TryGetValue(nodeIdentifier, out connection))
            {
                nodeToConnectionMap.Remove(nodeIdentifier);
                peerConnectionAction = RemovePeerNode(connection);
                nodeMessageHubs.Remove(nodeIdentifier);
                Bcl.HashSet<ReceiverIdentifier> actors;
                if (nodeActors.TryGetValue(nodeIdentifier, out actors))
                {
                    nodeActors.Remove(nodeIdentifier);

                    foreach (var actor in actors)
                    {
                        Bcl.HashSet<MessageIdentifier> messages;
                        if (actorToMessageMap.TryGetValue(actor, out messages))
                        {
                            actorToMessageMap.Remove(actor);

                            foreach (var message in messages)
                            {
                                HashedLinkedList<ReceiverIdentifier> nodes;
                                if (messageToNodeMap.TryGetValue(message, out nodes))
                                {
                                    nodes.Remove(nodeIdentifier);
                                    if (!nodes.Any())
                                    {
                                        messageToNodeMap.Remove(message);
                                    }
                                }
                            }
                        }
                    }
                }

                logger.Debug($"External route removed Uri:{connection.Node.Uri.AbsoluteUri} " +
                             $"Node:{nodeIdentifier.Identity.GetAnyString()}");
            }
            return new PeerRemoveResult
                   {
                       Uri = connection?.Node.Uri,
                       ConnectionAction = peerConnectionAction
                   };
        }

        private PeerConnectionAction RemovePeerNode(PeerConnection connection)
        {
            Bcl.HashSet<ReceiverIdentifier> nodes;
            var uri = connection.Node.Uri.ToSocketAddress();
            if (uriToNodeMap.TryGetValue(uri, out nodes))
            {
                if (nodes.Remove(new ReceiverIdentifier(connection.Node.SocketIdentity)))
                {
                    if (!nodes.Any())
                    {
                        uriToNodeMap.Remove(uri);

                        logger.Debug($"Zero nodes left registered at {uri}. Endpoint will be disconnected.");

                        return PeerConnectionAction.Disconnect;
                    }

                    logger.Debug($"[{nodes.Count}] node(s) left at {uri}.");

                    return PeerConnectionAction.KeepConnection;
                }
            }

            return PeerConnectionAction.NotFound;
        }

        public PeerRemoveResult RemoveMessageRoute(ExternalRouteRemoval routeRemoval)
        {
            PeerConnection connection = null;
            var connectionAction = PeerConnectionAction.NotFound;

            var nodeIdentifier = new ReceiverIdentifier(routeRemoval.Peer.SocketIdentity);
            if (nodeToConnectionMap.TryGetValue(nodeIdentifier, out connection))
            {
                if (routeRemoval.Route.Receiver.IsMessageHub())
                {
                    RemoveMessageHubRoute(routeRemoval);
                }
                else
                {
                    if (routeRemoval.Route.Receiver.IsActor())
                    {
                        RemoveActorRoute(routeRemoval);
                    }
                    else
                    {
                        RemoveActorsByMessage(routeRemoval, nodeIdentifier);
                    }
                }
                if (!nodeActors.ContainsKey(nodeIdentifier) && !nodeMessageHubs.ContainsKey(nodeIdentifier))
                {
                    nodeToConnectionMap.Remove(nodeIdentifier);
                    connectionAction = RemovePeerNode(connection);

                    logger.Debug($"External route removed Uri:{connection?.Node.Uri.AbsoluteUri} Node:{nodeIdentifier}");
                }
            }

            return new PeerRemoveResult
                   {
                       ConnectionAction = connectionAction,
                       Uri = connection?.Node.Uri
                   };
        }

        public Bcl.IEnumerable<NodeActors> FindAllActors(MessageIdentifier messageIdentifier)
        {
            var messageRoutes = new Bcl.List<NodeActors>();
            HashedLinkedList<ReceiverIdentifier> nodes;
            if (messageToNodeMap.TryGetValue(messageIdentifier, out nodes))
            {
                foreach (var node in nodes)
                {
                    Bcl.HashSet<ReceiverIdentifier> actors;
                    if (nodeActors.TryGetValue(node, out actors))
                    {
                        messageRoutes.Add(new NodeActors
                                          {
                                              NodeIdentifier = node,
                                              Actors = actorToMessageMap.Where(kv => actors.Contains(kv.Key)
                                                                                     && kv.Value.Contains(messageIdentifier))
                                                                        .Select(kv => kv.Key)
                                                                        .ToList()
                                          });
                    }
                }
            }
            return messageRoutes;
        }

        private void RemoveActorsByMessage(ExternalRouteRemoval routeRemoval, ReceiverIdentifier nodeIdentifier)
        {
            HashedLinkedList<ReceiverIdentifier> nodes;
            if (messageToNodeMap.TryGetValue(routeRemoval.Route.Message, out nodes))
            {
                if (nodes.Remove(nodeIdentifier))
                {
                    if (!nodes.Any())
                    {
                        messageToNodeMap.Remove(routeRemoval.Route.Message);
                    }
                    Bcl.HashSet<ReceiverIdentifier> actors;
                    var emptyActors = new Bcl.List<ReceiverIdentifier>();
                    if (nodeActors.TryGetValue(nodeIdentifier, out actors))
                    {
                        foreach (var actor in actors)
                        {
                            Bcl.HashSet<MessageIdentifier> messages;
                            if (actorToMessageMap.TryGetValue(actor, out messages))
                            {
                                if (messages.Remove(routeRemoval.Route.Message))
                                {
                                    if (!messages.Any())
                                    {
                                        actorToMessageMap.Remove(actor);
                                        emptyActors.Add(actor);
                                    }
                                }
                            }
                        }
                        foreach (var emptyActor in emptyActors)
                        {
                            actors.Remove(emptyActor);
                        }
                        if (!actors.Any())
                        {
                            nodeActors.Remove(nodeIdentifier);
                        }
                    }
                }
            }
        }

        private void RemoveActorRoute(ExternalRouteRemoval routeRemoval)
        {
            var nodeIdentifier = new ReceiverIdentifier(routeRemoval.Peer.SocketIdentity);
            Bcl.HashSet<MessageIdentifier> messages;
            if (actorToMessageMap.TryGetValue(routeRemoval.Route.Receiver, out messages))
            {
                messages.Remove(routeRemoval.Route.Message);
                if (!messages.Any())
                {
                    actorToMessageMap.Remove(routeRemoval.Route.Receiver);
                    RemoveNodeActor(routeRemoval, nodeIdentifier);
                }
                logger.Debug("External message route removed " +
                             $"Node:[{nodeIdentifier}] " +
                             $"Message:[{routeRemoval.Route.Message}]");
            }
        }

        private void RemoveNodeActor(ExternalRouteRemoval routeRemoval, ReceiverIdentifier nodeIdentifier)
        {
            Bcl.HashSet<ReceiverIdentifier> actors;
            if (nodeActors.TryGetValue(nodeIdentifier, out actors))
            {
                if (actors.Remove(routeRemoval.Route.Receiver))
                {
                    if (!actors.Any())
                    {
                        nodeActors.Remove(nodeIdentifier);
                    }
                    RemoveMessageToNodeMap(routeRemoval, nodeIdentifier);
                }
            }
        }

        private void RemoveMessageToNodeMap(ExternalRouteRemoval routeRemoval, ReceiverIdentifier receiverNode)
        {
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

        private void RemoveMessageHubRoute(ExternalRouteRemoval routeRemoval)
        {
            var nodeIdentifier = new ReceiverIdentifier(routeRemoval.Peer.SocketIdentity);
            Bcl.HashSet<ReceiverIdentifier> messageHubs;
            if (nodeMessageHubs.TryGetValue(nodeIdentifier, out messageHubs))
            {
                if (messageHubs.Remove(routeRemoval.Route.Receiver))
                {
                    if (!messageHubs.Any())
                    {
                        nodeMessageHubs.Remove(nodeIdentifier);
                    }
                    logger.Debug("External MessageHub removed " +
                                 $"Node:[{nodeIdentifier}] " +
                                 $"Identity:[{routeRemoval.Route.Receiver}]");
                }
            }
        }
    }
}