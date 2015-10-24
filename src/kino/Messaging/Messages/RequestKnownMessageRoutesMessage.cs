using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestKnownMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "REQKNOWNMSGROUTES".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}