using System;

namespace kino.Rendezvous.Configuration
{
    public class RendezvousConfiguration
    {
        public Uri BroadcastUri { get; set; }

        public Uri UnicastUri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}