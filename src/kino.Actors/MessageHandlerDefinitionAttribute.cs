using System;

namespace kino.Actors
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