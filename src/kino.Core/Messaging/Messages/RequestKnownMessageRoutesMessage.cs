using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RequestKnownMessageRoutesMessage : Payload
    {
        private static readonly byte[] MessageIdentity = BuildFullIdentity("REQKNOWNMSGROUTES");
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}