using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public class NodeActors
    {
        public ReceiverIdentifier NodeIdentifier { get; set; }

        public IEnumerable<ReceiverIdentifier> Actors { get; set; }
    }
}