using System.Collections.Generic;
using kino.Core;

namespace kino.Actors
{
    public class ActorRegistration
    {
        public ReceiverIdentifier ActorIdentifier { get; set; }

        public IEnumerable<ActorMessageHandlerIdentifier> MessageHandlers { get; set; }
    }
}