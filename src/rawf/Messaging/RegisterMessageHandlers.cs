using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging
{
    [ProtoContract]
    public class RegisterMessageHandlers : Payload
    {
        public static readonly byte[] MessageIdentity = "MSGHREG".GetBytes();

        [ProtoMember(1)]
        public MessageHandlerRegistration[] Registrations { get; set; }

        [ProtoMember(2)]
        public byte[] SocketIdentity { get; set; }
    }
}