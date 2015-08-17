using System;

namespace rawf.Rendezvous.Consensus
{
    public class OwnerEndpoint
    {
        public Uri MulticastUri { get; set; }
        public Uri UnicastUri { get; set; }
    }
}