using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestMessageExternalRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQMSGEXTROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract MessageContract { get; set; }

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}