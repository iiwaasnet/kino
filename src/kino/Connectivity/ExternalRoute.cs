using System.Collections.Generic;

namespace kino.Connectivity
{
    public class ExternalRoute
    {
        public Node Node { get; set; }
        public IEnumerable<MessageIdentifier> Messages { get; set; }
    }
}