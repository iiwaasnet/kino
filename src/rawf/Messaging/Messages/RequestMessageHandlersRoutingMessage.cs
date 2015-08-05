using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RequestMessageHandlersRoutingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REQROUTE".GetBytes();

        [ProtoMember(1)]
        public string RequestorUri { get; set; }

        [ProtoMember(2)]
        public byte[] RequestorSocketIdentity { get; set; }
    }
}