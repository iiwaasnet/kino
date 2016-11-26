using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RegisterExternalMessageRouteMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REGEXTROUTE");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] ReceiverNodeIdentity { get; set; }

        [ProtoMember(3)]
        public RouteRegistration[] Routes { get; set; }

        [ProtoMember(4)]
        public Health Health { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}