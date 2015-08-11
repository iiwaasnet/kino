using System;
using System.Collections.Generic;
using System.Linq;
using rawf.Framework;

namespace rawf.Connectivity
{
    public class ExternalRoutingTable : IExternalRoutingTable
    {
        private readonly IDictionary<MessageHandlerIdentifier, HashSet<SocketIdentifier>> messageHandlersMap;
        private readonly IDictionary<SocketIdentifier, HashSet<MessageHandlerIdentifier>> socketToMessageMap;
        private readonly IDictionary<SocketIdentifier, Uri> socketToUriMap;

        public ExternalRoutingTable()
        {
            messageHandlersMap = new Dictionary<MessageHandlerIdentifier, HashSet<SocketIdentifier>>();
            socketToMessageMap = new Dictionary<SocketIdentifier, HashSet<MessageHandlerIdentifier>>();
            socketToUriMap = new Dictionary<SocketIdentifier, Uri>();
        }

        public void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier, Uri uri)
        {
            var mapped = MapMessageToSocket(messageHandlerIdentifier, socketIdentifier);

            if (mapped)
            {
                socketToUriMap[socketIdentifier] = uri;

                MapSocketToMessage(messageHandlerIdentifier, socketIdentifier);

                Console.WriteLine($"Route added URI:{uri.AbsoluteUri} SOCKID:{socketIdentifier.Identity.GetString()}");
            }
        }

        private bool MapMessageToSocket(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashSet<SocketIdentifier> hashSet;
            if (!messageHandlersMap.TryGetValue(messageHandlerIdentifier, out hashSet))
            {
                hashSet = new HashSet<SocketIdentifier>();
                messageHandlersMap[messageHandlerIdentifier] = hashSet;
            }
            return hashSet.Add(socketIdentifier);
        }

        private void MapSocketToMessage(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier)
        {
            HashSet<MessageHandlerIdentifier> hashSet;
            if (!socketToMessageMap.TryGetValue(socketIdentifier, out hashSet))
            {
                hashSet = new HashSet<MessageHandlerIdentifier>();
                socketToMessageMap[socketIdentifier] = hashSet;
            }
            hashSet.Add(messageHandlerIdentifier);
        }

        public SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier)
        {
            //TODO: Implement round robin
            HashSet<SocketIdentifier> collection;
            return messageHandlersMap.TryGetValue(messageHandlerIdentifier, out collection)
                       ? Get(collection)
                       : null;
        }

        private static T Get<T>(ICollection<T> hashSet)
            => hashSet.Any()
                   ? hashSet.First()
                   : default(T);

        public void RemoveRoute(SocketIdentifier socketIdentifier)
        {
            socketToUriMap.Remove(socketIdentifier);

            HashSet<MessageHandlerIdentifier> messageHandlers;
            if (socketToMessageMap.TryGetValue(socketIdentifier, out messageHandlers))
            {
                foreach (var messageHandlerIdentifier in messageHandlers)
                {
                    HashSet<SocketIdentifier> socketIdentifiers;
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