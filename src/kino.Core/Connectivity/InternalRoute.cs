using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public class InternalRoute
    {
        public SocketIdentifier Socket { get; set; }
        public IEnumerable<MessageIdentifier> Messages { get; set; }
    }
}