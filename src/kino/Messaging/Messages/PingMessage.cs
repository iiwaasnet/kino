using System;
using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class PingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "PING".GetBytes();

        [ProtoMember(1)]
        public TimeSpan PingInterval { get; set; }

        [ProtoMember(2)]
        public ulong PingId { get; set; }
    }
}