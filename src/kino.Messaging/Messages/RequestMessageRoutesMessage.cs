using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQMSGROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract MessageContract { get; set; }

        [ProtoMember(2)]
        public RouteType RouteType { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}