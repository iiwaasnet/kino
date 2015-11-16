using System;

namespace kino.Core.Connectivity
{
    public class MessageHandlerDefinitionAttribute : Attribute
    {
        public MessageHandlerDefinitionAttribute(Type messageType)
        {
            MessageType = messageType;
        }

        public Type MessageType { get; }
    }
}