using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNode
    {
        [ProtoMember(1)]
        public string BroadcastUri { get; set; }

        [ProtoMember(2)]
        public string UnicastUri { get; set; }

        [ProtoMember(3)]
        public string PartnerBroadcastUri { get; set; }
    }
}