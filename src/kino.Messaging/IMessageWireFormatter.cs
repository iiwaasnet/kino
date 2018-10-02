using System.Collections.Generic;

namespace kino.Messaging
{
    public interface IMessageWireFormatter
    {
        IList<byte[]> Serialize(IMessage message);

        IMessage Deserialize(IList<byte[]> frames);
    }
}