using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class ReceiverRegistration
    {
        [ProtoMember(1)]
        public byte[] Identity { get; set; }

        [ProtoMember(2)]
        public bool LocalRegistration { get; set; }
    }
}