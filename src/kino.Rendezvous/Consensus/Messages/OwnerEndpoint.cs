using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class OwnerEndpoint
    {
        [ProtoMember(1)]
        public string UnicastUri { get; set; }
        [ProtoMember(2)]
        public string MulticastUri { get; set; }
    }
}