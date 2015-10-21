using System.Collections.Generic;
using kino.Messaging;

namespace kino.Connectivity
{
    public class ExternalRoute
    {
        public Node Node { get; set; }
        public IEnumerable<IMessageIdentifier> Messages { get; set; }
    }
}