using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class DeletePeerMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("DELETEPEER");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public byte[] NodeIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}