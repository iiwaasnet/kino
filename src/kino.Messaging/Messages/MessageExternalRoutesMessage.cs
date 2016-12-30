using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageExternalRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("MSGEXTROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract MessageContract { get; set; }

        [ProtoMember(2)]
        public ExternalRoute[] Routes { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}