using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("MSGROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract MessageContract { get; set; }

        [ProtoMember(2)]
        public MessageReceivingRoute[] ExternalRoutes { get; set; }

        [ProtoMember(3)]
        public MessageReceivingRoute[] InternalRoutes { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}