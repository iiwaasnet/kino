using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class CheckPeerConnectionMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("CHCKPEERCON");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public byte[] SocketIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}