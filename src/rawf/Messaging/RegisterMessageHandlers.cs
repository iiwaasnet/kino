using ProtoBuf;
using rawf.Framework;

namespace rawf.Messaging
{
    [ProtoContract]
    public class RegisterMessageHandlers : IPayload
    {
        public static readonly byte[] MessageIdentity = "MSGHREG".GetBytes();
        [ProtoMember(1)]
        public MessageHandlerRegistration[] Registrations { get; set; }
    }
}