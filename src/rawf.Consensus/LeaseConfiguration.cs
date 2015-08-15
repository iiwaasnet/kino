using System;

namespace rawf.Consensus
{
    public class LeaseConfiguration : ILeaseConfiguration
    {
        public TimeSpan MaxLeaseTimeSpan { get; set; }
        public TimeSpan ClockDrift { get; set; }
        public TimeSpan MessageRoundtrip { get; set; }
        public TimeSpan NodeResponseTimeout { get; set; }
    }
}