using System.Collections.Generic;

namespace kino.Messaging
{
    public interface IMessageWireFormatterProvider
    {
        IMessageWireFormatter GetWireFormatter(IList<byte[]> frames);
    }
}