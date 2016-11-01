using System;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class PingMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("PING");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public TimeSpan PingInterval { get; set; }

        [ProtoMember(2)]
        public ulong PingId { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}