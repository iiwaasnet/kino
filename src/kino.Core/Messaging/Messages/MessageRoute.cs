using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class MessageRoute
    {
        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public bool Connected { get; set; }

        [ProtoMember(3)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(4)]
        public MessageContract[] MessageContracts { get; set; }
    }
}