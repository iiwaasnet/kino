using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public class MessageActorRoute
    {
        public MessageIdentifier Message { get; set; }

        public IEnumerable<ReceiverIdentifierRegistration> Actors { get; set; }
    }
}