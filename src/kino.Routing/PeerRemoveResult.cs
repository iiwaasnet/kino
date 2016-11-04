using System;

namespace kino.Routing
{
    public class PeerRemoveResult
    {
        public Uri Uri { get; set; }

        public PeerConnectionAction ConnectionAction { get; set; }
    }
}