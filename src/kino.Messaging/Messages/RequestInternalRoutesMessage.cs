using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestInternalRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQINTROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}