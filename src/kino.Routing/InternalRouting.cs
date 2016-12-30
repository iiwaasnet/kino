using System.Collections.Generic;

namespace kino.Routing
{
    public class InternalRouting
    {
        public IEnumerable<MessageHubRoute> MessageHubs { get; set; }

        public IEnumerable<MessageActorRoute> Actors { get; set; }
    }
}