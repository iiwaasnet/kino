using System;

namespace rawf.Rendezvous
{
    public class RendezvousConfiguration : IRendezvousConfiguration
    {
        public Uri MulticastUri { get; set; }
        public Uri UnicastUri { get; set; }
        public TimeSpan PingInterval { get; set; }
    }
}