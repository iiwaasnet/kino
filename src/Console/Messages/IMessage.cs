using System.Collections.Generic;

namespace Console.Messages
{
    public interface IMessage
    {
        byte[] Content { get; }
        string Type { get; }
    }
}