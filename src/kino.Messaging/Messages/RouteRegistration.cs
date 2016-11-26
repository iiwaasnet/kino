using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RouteRegistration
    {
        [ProtoMember(1)]
        public byte[] ReceiverIdentifier { get; set; }

        [ProtoMember(2)]
        public MessageContract[] MessageContracts { get; set; }
    }
}