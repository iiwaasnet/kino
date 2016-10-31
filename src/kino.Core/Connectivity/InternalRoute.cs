using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class InternalRoute
    {
        public ILocalSendingSocket<IMessage> Socket { get; set; }

        public IEnumerable<Identifier> Messages { get; set; }
    }
}