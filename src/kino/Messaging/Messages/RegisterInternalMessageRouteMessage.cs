using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RegisterInternalMessageRouteMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REGINTROUTE".GetBytes();

        [ProtoMember(1)]
        public MessageContract[] MessageContracts { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        public override byte[] Version => Message.CurrentVersion;
        public override byte[] Identity => MessageIdentity;
    }
}