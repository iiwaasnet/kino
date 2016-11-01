using System.Collections.Generic;
using kino.Connectivity;
using kino.Core.Connectivity;
using kino.Messaging;

namespace kino.Routing
{
    public class InternalRoute
    {
        public ILocalSendingSocket<IMessage> Socket { get; set; }

        public IEnumerable<Identifier> Messages { get; set; }
    }
}