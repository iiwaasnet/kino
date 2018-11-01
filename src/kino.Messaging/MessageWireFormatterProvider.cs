using System.Collections.Generic;
using System.Linq;

namespace kino.Messaging
{
    public class MessageWireFormatterProvider : IMessageWireFormatterProvider
    {
        private readonly IEnumerable<IMessageWireFormatter> formatters;

        public MessageWireFormatterProvider(IEnumerable<IMessageWireFormatter> formatters)
            => this.formatters = formatters;

        public IMessageWireFormatter GetWireFormatter(IList<byte[]> frames)
            => formatters.First(f => f.CanDeserialize(frames));
    }
}