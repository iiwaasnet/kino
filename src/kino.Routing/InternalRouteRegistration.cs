using System.Collections.Generic;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;

namespace kino.Routing
{
    public class InternalRouteRegistration
    {
        public ReceiverIdentifier ReceiverIdentifier { get; set; }

        public bool KeepRegistrationLocal { get; set; }

        public IEnumerable<MessageContract> MessageContracts { get; set; }

        public ILocalSendingSocket<IMessage> DestinationSocket { get; set; }
    }
}