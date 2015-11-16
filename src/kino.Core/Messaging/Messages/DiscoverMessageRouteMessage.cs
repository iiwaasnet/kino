using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class DiscoverMessageRouteMessage : Payload
    {        
        private static readonly byte[] MessageIdentity = "DISCOVMSGROUTE".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public byte[] RequestorSocketIdentity { get; set; }

        [ProtoMember(2)]
        public string RequestorUri { get; set; }

        [ProtoMember(3)]
        public MessageContract MessageContract { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}