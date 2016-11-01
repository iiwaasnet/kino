using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestKnownMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQKNOWNMSGROUTES");
        private static readonly ushort MessageVersion = Message.CurrentVersion;

        public override ushort Version => MessageVersion;

        public override byte[] Identity => MessageIdentity;
    }
}