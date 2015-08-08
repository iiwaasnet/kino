using System;

namespace rawf.Rendezvous
{
    public class ApplicationConfiguration
    {
        public string BroadcastUri { get; set; }
        public string UnicastUri { get; set; }
        public TimeSpan PingInterval { get; set; }
    }
}