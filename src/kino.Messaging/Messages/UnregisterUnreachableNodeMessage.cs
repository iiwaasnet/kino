using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class UnregisterUnreachableNodeMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("UNREGUNREACHABLENODE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}