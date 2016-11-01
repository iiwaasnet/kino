using System.Collections.Generic;
using kino.Connectivity;
using kino.Core.Connectivity;
using kino.Messaging;

namespace kino.Routing
{
    public class InternalRouteRegistration
    {
        public IEnumerable<MessageContract> MessageContracts { get; set; }

        public ILocalSendingSocket<IMessage> DestinationSocket { get; set; }
    }
}