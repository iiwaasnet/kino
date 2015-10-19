using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestKnownMessageRoutesMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQKNOWNMSGROUTES".GetBytes();

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}