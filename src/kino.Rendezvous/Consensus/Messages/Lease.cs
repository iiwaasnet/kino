using ProtoBuf;

namespace kino.Rendezvous.Consensus.Messages
{
    [ProtoContract]
    public class Lease
    {
        [ProtoMember(1)]
        public byte[] Identity { get; set; }

        [ProtoMember(2)]
        public long ExpiresAt { get; set; }

        [ProtoMember(3)]
        public OwnerEndpoint OwnerEndpoint { get; set; }
    }
}