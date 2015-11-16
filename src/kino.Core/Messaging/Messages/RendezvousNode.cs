using ProtoBuf;

namespace kino.Core.Messaging.Messages
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