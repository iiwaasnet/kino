using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageContract
    {
        [ProtoMember(1)]
        public byte[] Version { get; set; }

        [ProtoMember(2)]
        public byte[] Identity { get; set; }
    }
}