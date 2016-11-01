using System;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class HeartBeatMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("HEARTBEAT");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public TimeSpan HeartBeatInterval { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}