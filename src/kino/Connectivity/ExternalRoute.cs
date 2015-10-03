using System;
using System.Collections.Generic;

namespace kino.Connectivity
{
    public class ExternalRoute
    {
        public Uri Node { get; set; }
        public SocketIdentifier Socket { get; set; }
        public IEnumerable<MessageIdentifier> Messages { get; set; }
    }
}