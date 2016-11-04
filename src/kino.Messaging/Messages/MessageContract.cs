using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageContract
    {
        [ProtoMember(1)]
        public ushort Version { get; set; }

        [ProtoMember(2)]
        public byte[] Identity { get; set; }

        [ProtoMember(3)]
        public byte[] Partition { get; set; }

        [ProtoMember(4)]
        public bool IsAnyIdentifier { get; set; }
    }
}