using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging.Messages
{
    [ProtoContract]
    public class RegisterMessageHandlersMessage : Payload
    {
        public static readonly byte[] MessageIdentity = "REGMSGH".GetBytes();

        [ProtoMember(1)]
        public MessageHandlerRegistration[] MessageHandlers { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }
    }
}