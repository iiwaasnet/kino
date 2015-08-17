using System;

namespace rawf.Rendezvous.Consensus
{
    public class LeaseConfiguration : ILeaseConfiguration
    {
        public TimeSpan MaxLeaseTimeSpan { get; set; }
        public TimeSpan ClockDrift { get; set; }
        public TimeSpan MessageRoundtrip { get; set; }
        public TimeSpan NodeResponseTimeout { get; set; }
    }
}