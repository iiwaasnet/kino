using System;

namespace kino.Rendezvous
{
    public class RendezvousConfiguration
    {
        public Uri MulticastUri { get; set; }
        public Uri UnicastUri { get; set; }
        public TimeSpan PingInterval { get; set; }
    }
}