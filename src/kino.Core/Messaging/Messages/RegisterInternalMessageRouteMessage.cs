using kino.Core.Framework;
using ProtoBuf;

namespace kino.Core.Messaging.Messages
{
    [ProtoContract]
    public class RegisterInternalMessageRouteMessage : Payload
    {
        private static readonly byte[] MessageIdentity = "REGINTROUTE".GetBytes();
        private static readonly byte[] MessageVersion = Message.CurrentVersion;

        [ProtoMember(1)]
        public MessageContract[] MessageContracts { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        public override byte[] Version => MessageVersion;
        public override byte[] Identity => MessageIdentity;
    }
}