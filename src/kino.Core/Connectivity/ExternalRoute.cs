using System.Collections.Generic;
using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public class ExternalRoute
    {
        public PeerConnection Connection { get; set; }

        public IEnumerable<Identifier> Messages { get; set; }
    }
}