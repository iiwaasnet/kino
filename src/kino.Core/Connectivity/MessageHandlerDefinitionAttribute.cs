using System;

namespace kino.Core.Connectivity
{
    public class MessageHandlerDefinitionAttribute : Attribute
    {
        public MessageHandlerDefinitionAttribute(Type messageType, bool keepRegistrationLocal = false)
        {
            KeepRegistrationLocal = keepRegistrationLocal;
            MessageType = messageType;
        }

        public bool KeepRegistrationLocal { get; }

        public Type MessageType { get; }
    }
}