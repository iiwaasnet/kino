using ProtoBuf;

namespace rawf.Consensus.Messages
{
    [ProtoContract]
    public class Lease
    {
        [ProtoMember(1)]
        public byte[] Identity { get; set; }

        [ProtoMember(2)]
        public long ExpiresAt { get; set; }

        [ProtoMember(3)]
        public string Uri { get; set; }
    }
}