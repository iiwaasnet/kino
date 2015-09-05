using System;

namespace kino.Rendezvous.Configuration
{
    public class ApplicationConfiguration
    {
        public string ServiceName { get; set; }
        public string BroadcastUri { get; set; }
        public string UnicastUri { get; set; }
        public TimeSpan PingInterval { get; set; }
        public SynodConfiguration Synod { get; set; }
    }
}