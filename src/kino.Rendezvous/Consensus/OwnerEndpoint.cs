using System;

namespace kino.Rendezvous.Consensus
{
    public class OwnerEndpoint
    {
        public Uri MulticastUri { get; set; }
        public Uri UnicastUri { get; set; }
    }
}