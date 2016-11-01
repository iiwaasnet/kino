using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RegisterInternalMessageRouteMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REGINTROUTE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract[] MessageContracts { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}