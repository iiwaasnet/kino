using System.Collections.Generic;
using kino.Core;

namespace kino.Routing
{
    public class ExternalRoute
    {
        public PeerConnection Connection { get; set; }

        public IEnumerable<Identifier> Messages { get; set; }
    }
}