using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RegisterInternalMessageRouteMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REGINTROUTE");
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract[] GlobalMessageContracts { get; set; }

        [ProtoMember(2)]
        public MessageContract[] LocalMessageContracts { get; set; }

        [ProtoMember(3)]
        public byte[] SocketIdentity { get; set; }

        public override byte[] Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}