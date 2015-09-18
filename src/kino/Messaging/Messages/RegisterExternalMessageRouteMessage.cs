using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RegisterExternalMessageRouteMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REGEXTROUTE".GetBytes();

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageContract[] MessageContracts { get; set; }
    }
}