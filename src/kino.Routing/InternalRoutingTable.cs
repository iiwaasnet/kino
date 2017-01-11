using System;
using System.Linq;
using C5;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using Bcl = System.Collections.Generic;

namespace kino.Routing
{
    public class InternalRoutingTable : IInternalRoutingTable
    {
        private readonly Bcl.IDictionary<ReceiverIdentifier, ILocalSendingSocket<IMessage>> messageHubs;
        private readonly Bcl.IDictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>> messageToActorMap;
        private readonly Bcl.IDictionary<ReceiverIdentifier, ILocalSendingSocket<IMessage>> actorToSocketMap;
        private readonly Bcl.IDictionary<ILocalSendingSocket<IMessage>, Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>>> socketToActorMessagesMap;

        public InternalRoutingTable()
        {
            messageHubs = new Bcl.Dictionary<ReceiverIdentifier, ILocalSendingSocket<IMessage>>();
            messageToActorMap = new Bcl.Dictionary<MessageIdentifier, HashedLinkedList<ReceiverIdentifier>>();
            actorToSocketMap = new Bcl.Dictionary<ReceiverIdentifier, ILocalSendingSocket<IMessage>>();
            socketToActorMessagesMap = new Bcl.Dictionary<ILocalSendingSocket<IMessage>, Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>>>();
        }

        public void AddMessageRoute(InternalRouteRegistration routeRegistration)
        {
            if (routeRegistration.ReceiverIdentifier.IsMessageHub())
            {
                var registration = new ReceiverIdentifierRegistration(routeRegistration.ReceiverIdentifier,
                                                                      routeRegistration.KeepRegistrationLocal);
                messageHubs[registration] = routeRegistration.DestinationSocket;
            }
            else
            {
                if (routeRegistration.ReceiverIdentifier.IsActor())
                {
                    var actorMessages = MapSocketToActor(routeRegistration);
                    foreach (var messageContract in routeRegistration.MessageContracts)
                    {
                        MapMessageToActor(routeRegistration, messageContract);
                        MapActorToMessage(routeRegistration, actorMessages, messageContract);
                    }
                    actorToSocketMap[routeRegistration.ReceiverIdentifier] = routeRegistration.DestinationSocket;
                }
                else
                {
                    throw new ArgumentException($"Requested registration is for unknown Receiver type: [{routeRegistration.ReceiverIdentifier}]!");
                }
            }
        }

        private Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>> MapSocketToActor(InternalRouteRegistration routeRegistration)
        {
            Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>> actorMessages;
            if (!socketToActorMessagesMap.TryGetValue(routeRegistration.DestinationSocket, out actorMessages))
            {
                actorMessages = new Bcl.Dictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>>
                                {
                                    [routeRegistration.ReceiverIdentifier] = new Bcl.HashSet<MessageIdentifier>()
                                };
                socketToActorMessagesMap[routeRegistration.DestinationSocket] = actorMessages;
            }
            return actorMessages;
        }

        private static void MapActorToMessage(InternalRouteRegistration routeRegistration,
                                              Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>> actorMessages,
                                              MessageContract messageContract)
        {
            Bcl.HashSet<MessageIdentifier> messages;
            if (!actorMessages.TryGetValue(routeRegistration.ReceiverIdentifier, out messages))
            {
                messages = new Bcl.HashSet<MessageIdentifier>();
                actorMessages[routeRegistration.ReceiverIdentifier] = messages;
            }
            messages.Add(messageContract.Message);
        }

        private void MapMessageToActor(InternalRouteRegistration routeRegistration, MessageContract messageContract)
        {
            HashedLinkedList<ReceiverIdentifier> actors;
            if (!messageToActorMap.TryGetValue(messageContract.Message, out actors))
            {
                actors = new HashedLinkedList<ReceiverIdentifier>();
                messageToActorMap[messageContract.Message] = actors;
            }
            if (!actors.Contains(routeRegistration.ReceiverIdentifier))
            {
                var registration = new ReceiverIdentifierRegistration(routeRegistration.ReceiverIdentifier,
                                                                      messageContract.KeepRegistrationLocal);
                actors.InsertLast(registration);
            }
        }

        public Bcl.IEnumerable<ILocalSendingSocket<IMessage>> FindRoutes(InternalRouteLookupRequest lookupRequest)
        {
            HashedLinkedList<ReceiverIdentifier> actors;
            var sockets = new Bcl.List<ILocalSendingSocket<IMessage>>();
            ILocalSendingSocket<IMessage> socket;
            if (lookupRequest.ReceiverIdentity.IsSet())
            {
                if (lookupRequest.ReceiverIdentity.IsMessageHub())
                {
                    if (messageHubs.TryGetValue(lookupRequest.ReceiverIdentity, out socket))
                    {
                        sockets.Add(socket);
                    }
                }
                else
                {
                    if (lookupRequest.ReceiverIdentity.IsActor())
                    {
                        if (actorToSocketMap.TryGetValue(lookupRequest.ReceiverIdentity, out socket))
                        {
                            if (messageToActorMap.TryGetValue(lookupRequest.Message, out actors))
                            {
                                if (actors.Contains(lookupRequest.ReceiverIdentity))
                                {
                                    sockets.Add(socket);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (messageToActorMap.TryGetValue(lookupRequest.Message, out actors))
                {
                    if (lookupRequest.Distribution == DistributionPattern.Unicast)
                    {
                        if (actorToSocketMap.TryGetValue(Get(actors), out socket))
                        {
                            sockets.Add(socket);
                        }
                    }
                    else
                    {
                        if (lookupRequest.Distribution == DistributionPattern.Broadcast)
                        {
                            foreach (var actor in actors)
                            {
                                if (actorToSocketMap.TryGetValue(actor, out socket))
                                {
                                    sockets.Add(socket);
                                }
                            }
                        }
                    }
                }
            }

            return sockets;
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

        public Bcl.IEnumerable<MessageRoute> RemoveReceiverRoute(ILocalSendingSocket<IMessage> receivingSocket)
            => RemoveActors(receivingSocket)
                .Concat(RemoveMessageHub(receivingSocket))
                .ToList();

        private Bcl.IEnumerable<MessageRoute> RemoveMessageHub(ILocalSendingSocket<IMessage> receivingSocket)
        {
            // Should not be many, we can iterate the collection
            var messageHub = messageHubs.Where(kv => kv.Value.Equals(receivingSocket))
                                        .Select(kv => kv.Key)
                                        .FirstOrDefault();
            if (messageHub != null)
            {
                messageHubs.Remove(messageHub);

                yield return new MessageRoute {Receiver = messageHub};
            }
        }

        private Bcl.IEnumerable<MessageRoute> RemoveActors(ILocalSendingSocket<IMessage> receivingSocket)
        {
            Bcl.IDictionary<ReceiverIdentifier, Bcl.HashSet<MessageIdentifier>> actorMessages;
            if (socketToActorMessagesMap.TryGetValue(receivingSocket, out actorMessages))
            {
                socketToActorMessagesMap.Remove(receivingSocket);
                foreach (var actor in actorMessages.Keys)
                {
                    actorToSocketMap.Remove(actor);
                    foreach (var message in actorMessages[actor])
                    {
                        HashedLinkedList<ReceiverIdentifier> messageHandlers;
                        if (messageToActorMap.TryGetValue(message, out messageHandlers))
                        {
                            messageHandlers.Remove(actor);
                            if (!messageHandlers.Any())
                            {
                                messageToActorMap.Remove(message);
                            }
                        }

                        yield return new MessageRoute {Message = message, Receiver = actor};
                    }
                }
            }
        }

        public InternalRouting GetAllRoutes()
            => new InternalRouting
               {
                   MessageHubs = messageHubs.Keys
                                            .OfType<ReceiverIdentifierRegistration>()
                                            .Select(mh => new MessageHubRoute
                                                          {
                                                              MessageHub = mh,
                                                              LocalRegistration = mh.LocalRegistration
                                                          })
                                            .ToList(),
                   Actors = messageToActorMap.Select(messageActors => new MessageActorRoute
                                                                      {
                                                                          Message = messageActors.Key,
                                                                          Actors = messageActors.Value
                                                                                                .OfType<ReceiverIdentifierRegistration>()
                                                                                                .ToList()
                                                                      })
                                             .ToList()
               };
    }
}