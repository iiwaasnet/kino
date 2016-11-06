using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class AddPeerMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("ADDPEER");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(2)]
        public string Uri { get; set; }

        [ProtoMember(3)]
        public Health Health { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}