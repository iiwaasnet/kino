using System;

namespace kino.Rendezvous.Configuration
{
    public class RendezvousConfiguration
    {
        public string BroadcastUri { get; set; }

        public string UnicastUri { get; set; }

        public TimeSpan HeartBeatInterval { get; set; }
    }
}