using ProtoBuf;

namespace rawf.Consensus.Messages
{
    [ProtoContract]
    public class Node
    {
        [ProtoMember(1)]
        public string Uri { get; set; }
        [ProtoMember(1)]
        public byte[] SocketIdentity { get; set; }
    }
}