using System;
using Console.Messages;

namespace Console
{
    public class MessageMap
    {
        public Func<IMessage, IMessage> Handler { get; set; }
        public MessageDefinition Message { get; set; }
    }

    public class MessageDefinition
    {
        public string Type { get; set; }
    }
}