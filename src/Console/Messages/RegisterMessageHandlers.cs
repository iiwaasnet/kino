using System.Collections.Generic;

namespace Console.Messages
{
    public class RegisterMessageHandlers : IPayload
    {
        public const string MessageIdentity = "MSGHREG";
        public IEnumerable<MessageHandlerRegistration> Registrations { get; set; }
    }
}