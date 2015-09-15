using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RequestAllMessageRoutingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQALLROUTE".GetBytes();

        [ProtoMember(1)]
        public string RequestorUri { get; set; }

        [ProtoMember(2)]
        public byte[] RequestorSocketIdentity { get; set; }                
    }
}