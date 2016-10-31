using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class InternalRouteRegistration
    {
        public IEnumerable<MessageContract> MessageContracts { get; set; }

        public ILocalSendingSocket<IMessage> DestinationSocket { get; set; }
    }
}