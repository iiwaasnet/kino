using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class UnregisterNodeMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("UNREGNODE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}