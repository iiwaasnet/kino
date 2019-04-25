using System;
using System.Collections.Generic;
using kino.Core;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public class WireMessage
    {
        public byte[] Identity { get; set; }

        public ushort Version { get; set; }

        public byte[] Partition { get; set; }

        public byte[] Body { get; set; }

        public TimeSpan TTL { get; set; }

        public byte[] CorrelationId { get; set; }

        public byte[] ReceiverIdentity { get; set; }

        public byte[] ReceiverNodeIdentity { get; set; }

        public byte[] Signature { get; set; }

        public IEnumerable<MessageIdentifier> CallbackPoint { get; set; }

        public long CallbackKey { get; set; }

        public byte[] CallbackReceiverIdentity { get; set; }

        public byte[] CallbackReceiverNodeIdentity { get; set; }

        public byte[] SocketIdentity { get; set; }

        public MessageTraceOptions TraceOptions { get; set; }

        public DistributionPattern Distribution { get; set; }

        public ushort Hops { get; set; }

        public string Domain { get; set; }

        private List<SocketEndpoint> Routing { get; set; }
    }
}