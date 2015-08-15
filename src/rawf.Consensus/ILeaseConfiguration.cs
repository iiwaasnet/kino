using System;

namespace rawf.Consensus
{
    public interface ILeaseConfiguration
    {
        TimeSpan MaxLeaseTimeSpan { get; }
        TimeSpan ClockDrift { get; }
        TimeSpan MessageRoundtrip { get; }
        TimeSpan NodeResponseTimeout { get; }
    }
}