using ProtoBuf;

namespace rawf.Messaging
{
    [ProtoContract]
    public class RegisterMessageHandlers : IPayload
    {
        public const string MessageIdentity = "MSGHREG";
        [ProtoMember(1)]
        public MessageHandlerRegistration[] Registrations { get; set; }
    }
}