using System;
using System.Collections.Concurrent;
using System.Linq;
using C5;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using Bcl = System.Collections.Generic;

namespace kino.Routing.Kafka
{
    public class ExternalKafkaRoutingTable : IExternalRoutingTable
    {
        private readonly Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>> nodeMessageHubs;
        private readonly Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>> nodeActors;
        private readonly Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>> actorToMessageMap;
        private readonly Bcl.IDictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>> messageToNodeMap;
        private readonly Bcl.IDictionary<ReceiverIdentifier, KafkaPeerConnection> nodeToConnectionMap;
        private readonly Bcl.IDictionary<KafkaAppCluster, Bcl.HashSet<ReceiverIdentifier>> clusterToNodeMap;
        private readonly IRoundRobinDestinationList roundRobinList;
        private readonly ILogger logger;

        public ExternalKafkaRoutingTable(IRoundRobinDestinationList roundRobinList, ILogger logger)
        {
            this.roundRobinList = roundRobinList;
            this.logger = logger;
            nodeMessageHubs = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
            nodeActors = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
            actorToMessageMap = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>>();
            messageToNodeMap = new Bcl.Dictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>>();
            nodeToConnectionMap = new Bcl.Dictionary<ReceiverIdentifier, KafkaPeerConnection>();
            clusterToNodeMap = new ConcurrentDictionary<KafkaAppCluster, Bcl.HashSet<ReceiverIdentifier>>();
        }

        public KafkaPeerConnection AddMessageRoute(ExternalKafkaRouteRegistration routeRegistration)
        {
            var nodeIdentifier = new ReceiverIdentifier(routeRegistration.Node.NodeIdentity);

            if (routeRegistration.Route.Receiver.IsActor())
            {
                MapMessageToNode(routeRegistration, nodeIdentifier);
                MapActorToMessage(routeRegistration);
                MapActorToNode(routeRegistration, nodeIdentifier);

                logger.Debug("External route added "
                             + $"BrokerUri:{routeRegistration.Node.BrokerUri} "
                             + $"Topic:{routeRegistration.Node.Topic} "
                             + $"Queue:{routeRegistration.Node.Queue} "
                             + $"Node:{nodeIdentifier} "
                             + $"Actor:{routeRegistration.Route.Receiver}"
                             + $"Message:{routeRegistration.Route.Message}");
            }
            else
            {
                if (routeRegistration.Route.Receiver.IsMessageHub())
                {
                    MapMessageHubToNode(routeRegistration, nodeIdentifier);

                    logger.Debug("External route added "
                                 + $"BrokerUri:{routeRegistration.Node.BrokerUri} "
                                 + $"Topic:{routeRegistration.Node.Topic} "
                                 + $"Queue:{routeRegistration.Node.Queue} "
                                 + $"Node:{nodeIdentifier} "
                                 + $"MessageHub:{routeRegistration.Route.Receiver}");
                }
                else
                {
                    throw new ArgumentException($"Requested registration is for unknown Receiver type: [{routeRegistration.Route.Receiver}]!");
                }
            }

            // TODO: Think of better naming, hence this is not connection any more.
            var connection = MapNodeToConnection(routeRegistration, nodeIdentifier);
            MapClusterToNode(connection);

            return connection;
        }

        private void MapClusterToNode(KafkaPeerConnection connection)
        {
            var cluster = new KafkaAppCluster
                          {
                              BrokerUri = connection.Node.BrokerUri,
                              Topic = connection.Node.Topic,
                              Queue = connection.Node.Queue
                          };
            if (!clusterToNodeMap.TryGetValue(cluster, out var nodes))
            {
                nodes = new Bcl.HashSet<ReceiverIdentifier>();
                clusterToNodeMap[cluster] = nodes;
            }

            nodes.Add(new ReceiverIdentifier(connection.Node.NodeIdentity));

            logger.Debug($"[{nodes.Count}] node(s) registered at {cluster}.");
        }

        private KafkaPeerConnection MapNodeToConnection(ExternalKafkaRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            if (!nodeToConnectionMap.TryGetValue(nodeIdentifier, out var peerConnection))
            {
                peerConnection = new KafkaPeerConnection
                                 {
                                     Node = routeRegistration.Node,
                                     Health = routeRegistration.Health,
                                     Connected = false
                                 };
                nodeToConnectionMap[nodeIdentifier] = peerConnection;

                roundRobinList.Add(peerConnection.Node);
            }

            return peerConnection;
        }

        private void MapMessageHubToNode(ExternalKafkaRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            var messageHub = routeRegistration.Route.Receiver;
            if (!nodeMessageHubs.TryGetValue(nodeIdentifier, out var messageHubs))
            {
                messageHubs = new Bcl.HashSet<ReceiverIdentifier>();
                nodeMessageHubs[nodeIdentifier] = messageHubs;
            }

            messageHubs.Add(messageHub);
        }

        private void MapActorToMessage(ExternalKafkaRouteRegistration routeRegistration)
        {
            if (!actorToMessageMap.TryGetValue(routeRegistration.Route.Receiver, out var actorMessages))
            {
                actorMessages = new Bcl.HashSet<MessageIdentifier>();
                actorToMessageMap[routeRegistration.Route.Receiver] = actorMessages;
            }

            actorMessages.Add(routeRegistration.Route.Message);
        }

        private void MapActorToNode(ExternalKafkaRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            if (!nodeActors.TryGetValue(nodeIdentifier, out var actors))
            {
                actors = new Bcl.HashSet<ReceiverIdentifier>();
                nodeActors[nodeIdentifier] = actors;
            }

            actors.Add(routeRegistration.Route.Receiver);
        }

        private void MapMessageToNode(ExternalKafkaRouteRegistration routeRegistration, ReceiverIdentifier nodeIdentifier)
        {
            var messageIdentifier = routeRegistration.Route.Message;
            if (!messageToNodeMap.TryGetValue(messageIdentifier, out var nodes))
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
            var peers = new Bcl.List<PeerConnection>(20);
            if (lookupRequest.ReceiverNodeIdentity.IsSet())
            {
                if (nodeToConnectionMap.TryGetValue(lookupRequest.ReceiverNodeIdentity, out var peerConnection))
                {
                    peers.Add(peerConnection);
                }
            }
            else
            {
                if (messageToNodeMap.TryGetValue(lookupRequest.Message, out var nodes))
                {
                    if (lookupRequest.Distribution == DistributionPattern.Unicast)
                    {
                        peers.Add(nodeToConnectionMap[nodes.RoundRobinGet()]);
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

        public PeerRemoveResult RemoveNodeRoute(ReceiverIdentifier nodeIdentifier)
        {
            var peerConnectionAction = PeerConnectionAction.NotFound;

            if (nodeToConnectionMap.TryGetValue(nodeIdentifier, out var connection))
            {
                nodeToConnectionMap.Remove(nodeIdentifier);
                peerConnectionAction = RemovePeerNode(connection);
                roundRobinList.Remove(connection.Node);
                nodeMessageHubs.Remove(nodeIdentifier);
                if (nodeActors.TryGetValue(nodeIdentifier, out var actors))
                {
                    nodeActors.Remove(nodeIdentifier);

                    foreach (var actor in actors)
                    {
                        if (actorToMessageMap.TryGetValue(actor, out var messages))
                        {
                            actorToMessageMap.Remove(actor);

                            foreach (var message in messages)
                            {
                                if (messageToNodeMap.TryGetValue(message, out var nodes))
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

                logger.Debug($"External route removed Uri:{connection.Node.Uri} "
                             + $"Node:{nodeIdentifier.Identity.GetAnyString()}");
            }

            return new PeerRemoveResult
                   {
                       Uri = connection?.Node.Uri,
                       ConnectionAction = peerConnectionAction
                   };
        }

        private PeerConnectionAction RemovePeerNode(PeerConnection connection)
        {
            var uri = connection.Node.Uri;
            if (clusterToNodeMap.TryGetValue(uri, out var nodes))
            {
                if (nodes.Remove(new ReceiverIdentifier(connection.Node.SocketIdentity)))
                {
                    if (!nodes.Any())
                    {
                        clusterToNodeMap.Remove(uri);

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
            var connectionAction = PeerConnectionAction.NotFound;

            var nodeIdentifier = new ReceiverIdentifier(routeRemoval.NodeIdentifier);
            if (nodeToConnectionMap.TryGetValue(nodeIdentifier, out var connection))
            {
                connectionAction = PeerConnectionAction.KeepConnection;

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
                    roundRobinList.Remove(connection.Node);

                    logger.Debug($"External route removed Uri:{connection?.Node.Uri} Node:{nodeIdentifier}");
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
            if (messageToNodeMap.TryGetValue(messageIdentifier, out var nodes))
            {
                foreach (var node in nodes)
                {
                    if (nodeActors.TryGetValue(node, out var actors))
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
            if (messageToNodeMap.TryGetValue(routeRemoval.Route.Message, out var nodes))
            {
                if (nodes.Remove(nodeIdentifier))
                {
                    if (!nodes.Any())
                    {
                        messageToNodeMap.Remove(routeRemoval.Route.Message);
                    }

                    var emptyActors = new Bcl.List<ReceiverIdentifier>();
                    if (nodeActors.TryGetValue(nodeIdentifier, out var actors))
                    {
                        foreach (var actor in actors)
                        {
                            if (actorToMessageMap.TryGetValue(actor, out var messages))
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
            var nodeIdentifier = new ReceiverIdentifier(routeRemoval.NodeIdentifier);
            if (actorToMessageMap.TryGetValue(routeRemoval.Route.Receiver, out var messages))
            {
                messages.Remove(routeRemoval.Route.Message);
                if (!messages.Any())
                {
                    actorToMessageMap.Remove(routeRemoval.Route.Receiver);
                    RemoveNodeActor(routeRemoval, nodeIdentifier);
                }

                logger.Debug("External message route removed "
                             + $"Node:[{nodeIdentifier}] "
                             + $"Message:[{routeRemoval.Route.Message}]");
            }
        }

        private void RemoveNodeActor(ExternalRouteRemoval routeRemoval, ReceiverIdentifier nodeIdentifier)
        {
            if (nodeActors.TryGetValue(nodeIdentifier, out var actors))
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
            if (messageToNodeMap.TryGetValue(routeRemoval.Route.Message, out var nodes))
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
            var nodeIdentifier = new ReceiverIdentifier(routeRemoval.NodeIdentifier);
            if (nodeMessageHubs.TryGetValue(nodeIdentifier, out var messageHubs))
            {
                if (messageHubs.Remove(routeRemoval.Route.Receiver))
                {
                    if (!messageHubs.Any())
                    {
                        nodeMessageHubs.Remove(nodeIdentifier);
                    }

                    logger.Debug("External MessageHub removed "
                                 + $"Node:[{nodeIdentifier}] "
                                 + $"Identity:[{routeRemoval.Route.Receiver}]");
                }
            }
        }

        public Bcl.IEnumerable<ExternalRoute> GetAllRoutes()
            => clusterToNodeMap.SelectMany(uriNodes => uriNodes.Value.Select(node => (Identitifier: node, Uri: uriNodes.Key)))
                               .Select(node => new ExternalRoute
                                               {
                                                   Node = new Node(node.Uri, node.Identitifier.Identity),
                                                   MessageRoutes = GetNodeActors(node.Identitifier),
                                                   MessageHubs = GetMessageNodeHubs(node.Identitifier)
                                               })
                               .ToList();

        private Bcl.IEnumerable<MessageHubRoute> GetMessageNodeHubs(ReceiverIdentifier node)
            => nodeMessageHubs.TryGetValue(node, out var messageHubs)
                   ? messageHubs.Select(mh => new MessageHubRoute
                                              {
                                                  MessageHub = mh,
                                                  LocalRegistration = false
                                              })
                                .ToList()
                   : Enumerable.Empty<MessageHubRoute>();

        private Bcl.IEnumerable<MessageActorRoute> GetNodeActors(ReceiverIdentifier node)
        {
            return GetNodeMessageToActorsMap()
                   .Select(ma => new MessageActorRoute
                                 {
                                     Message = ma.Key,
                                     Actors = ma.Value
                                                .Select(a => new ReceiverIdentifierRegistration(a, false))
                                                .ToList()
                                 })
                   .ToList();

            Bcl.IDictionary<MessageIdentifier, Bcl.HashSet<ReceiverIdentifier>> GetNodeMessageToActorsMap()
            {
                var messageActors = new Bcl.Dictionary<MessageIdentifier, Bcl.HashSet<ReceiverIdentifier>>();
                if (nodeActors.TryGetValue(node, out var actors))
                {
                    foreach (var actor in actors)
                    {
                        if (actorToMessageMap.TryGetValue(actor, out var actorMessages))
                        {
                            foreach (var message in actorMessages)
                            {
                                if (!messageActors.TryGetValue(message, out var tmpActors))
                                {
                                    tmpActors = new Bcl.HashSet<ReceiverIdentifier>();
                                    messageActors[message] = tmpActors;
                                }

                                tmpActors.Add(actor);
                            }
                        }
                    }
                }

                return messageActors;
            }
        }
    }
}