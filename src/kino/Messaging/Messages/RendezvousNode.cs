using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RendezvousNode
    {
        [ProtoMember(1)]
        public string MulticastUri { get; set; }

        [ProtoMember(2)]
        public string UnicastUri { get; set; }
    }
}