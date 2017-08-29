using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public class ExternalRoute
    {
        public Node Node { get; set; }

        public IEnumerable<MessageHubRoute> MessageHubs { get; set; }

        public IEnumerable<MessageActorRoute> MessageRoutes { get; set; }
    }
}