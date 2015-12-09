using System.Collections.Generic;

namespace kino.Core.Connectivity
{
    public class ExternalRoute
    {
        public PeerConnection Connection { get; set; }
        public IEnumerable<MessageIdentifier> Messages { get; set; }
    }
}