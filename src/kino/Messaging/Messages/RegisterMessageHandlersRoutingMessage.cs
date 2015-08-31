using kino.Framework;
using ProtoBuf;

namespace kino.Messaging.Messages
{
    [ProtoContract]
    public class RegisterMessageHandlersRoutingMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REGROUTE".GetBytes();

        [ProtoMember(1)]
        public string Uri { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }

        [ProtoMember(3)]
        public MessageHandlerRegistration[] MessageHandlers { get; set; }
    }
}