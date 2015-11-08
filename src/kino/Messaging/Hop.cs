using ProtoBuf;

namespace kino.Messaging
{
    [ProtoContract]
    public class Hop
    {
        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] Identity { get; set; }
    }
}