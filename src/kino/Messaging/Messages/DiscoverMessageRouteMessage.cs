using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class DiscoverMessageRouteMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "DISCOVMSGROUTE".GetBytes();

        [ProtoMember(1)]
        public byte[] RequestorSocketIdentity { get; set; }

        [ProtoMember(2)]
        public string RequestorUri { get; set; }

        [ProtoMember(3)]
        public MessageContract MessageContract { get; set; }
    }
}