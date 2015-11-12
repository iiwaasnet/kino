using System;

namespace kino.Consensus.Configuration
{
    public class LeaseConfiguration
    {
        public TimeSpan MaxLeaseTimeSpan { get; set; }
        public TimeSpan ClockDrift { get; set; }
        public TimeSpan MessageRoundtrip { get; set; }
        public TimeSpan NodeResponseTimeout { get; set; }
    }
}