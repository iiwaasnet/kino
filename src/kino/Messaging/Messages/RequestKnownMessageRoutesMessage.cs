using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestKnownMessageRoutesMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQKNOWNMSGROUTES".GetBytes();
    }
}