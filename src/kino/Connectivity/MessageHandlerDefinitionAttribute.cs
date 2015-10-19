using System;
using kino.Messaging;

namespace kino.Connectivity
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