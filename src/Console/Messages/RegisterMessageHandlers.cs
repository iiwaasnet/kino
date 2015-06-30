using System.Collections.Generic;
using ProtoBuf;

namespace Console.Messages
{
    [ProtoContract]
    public class RegisterMessageHandlers : IPayload
    {
        public const string MessageIdentity = "MSGHREG";
        [ProtoMember(1)]
        public MessageHandlerRegistration[] Registrations { get; set; }
    }
}