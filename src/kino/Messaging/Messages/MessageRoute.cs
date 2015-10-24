using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class MessageRoute
    {
        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageContract[] MessageContracts { get; set; }
    }
}