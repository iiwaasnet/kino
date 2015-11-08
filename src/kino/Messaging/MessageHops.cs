using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Framework;

namespace kino.Messaging
{
    public class MessageHops
    {
        private static readonly IMessageSerializer serializer;
        private readonly List<SocketEndpoint> hops;

        static MessageHops()
        {
            serializer = new ProtobufMessageSerializer();
        }

        public MessageHops()
        {
            hops = new List<SocketEndpoint>();
        }

        public MessageHops(byte[] data)
        {
            hops = new List<SocketEndpoint>(FromBytes(data));
        }

        internal void Add(SocketEndpoint hop)
            => hops.Add(hop);

        internal void Clear()
            => hops.Clear();

        internal void AddRange(IEnumerable<SocketEndpoint> hops)
            => this.hops.AddRange(hops);

        private static IEnumerable<SocketEndpoint> FromBytes(byte[] data)
        {
            if (data?.Length > 0)
            {
                var hopsList = serializer.Deserialize<HopsList>(data);
                return hopsList.Hops.Select(hop => new SocketEndpoint(new Uri(hop.Uri), hop.Identity));
            }

            return Enumerable.Empty<SocketEndpoint>();
        }

        internal byte[] GetBytes()
        {
            if (hops.Any())
            {
                return serializer.Serialize(new HopsList
                                     {
                                         Hops = hops.Select(hop => new Hop
                                                                   {
                                                                       Uri = hop.Uri.ToSocketAddress(),
                                                                       Identity = hop.Identity
                                                                   })
                                     });
            }

            return new byte[0];
        }

        internal IEnumerable<SocketEndpoint> Hops => hops;
    }
}