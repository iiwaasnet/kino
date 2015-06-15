using System.Collections.Generic;

namespace Console.Messages
{
    public class Message : IMessage
    {
        private static readonly IMessageSerializer messageSerializer;

        static Message()
        {
            messageSerializer = new MessageSerializer();
        }

        protected Message()
        {
        }

        public Message(byte[] content, string msgType)
        {
            Content = content;
            Type = msgType;
        }

        protected byte[] Serialize(object payload)
            => messageSerializer.Serialize(payload);

        protected static T Deserialize<T>(byte[] content)
            => messageSerializer.Deserialize<T>(content);

        public byte[] Content { get; protected set; }
        public string Type { get; protected set; }
    }
}