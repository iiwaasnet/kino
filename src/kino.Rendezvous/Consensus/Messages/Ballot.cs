using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class Ballot
    {
        [ProtoMember(1)]
        public long Timestamp { get; set; }
        [ProtoMember(2)]
        public byte[] Identity { get; set; }
        [ProtoMember(3)]
        public int MessageNumber { get; set; }
    }
}