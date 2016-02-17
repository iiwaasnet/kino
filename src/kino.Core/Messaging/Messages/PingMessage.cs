using System;
using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class PingMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("PING");
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public TimeSpan PingInterval { get; set; }

        [ProtoMember(2)]
        public ulong PingId { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}