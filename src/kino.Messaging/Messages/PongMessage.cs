using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class PongMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("PONG");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public ulong PingId { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}