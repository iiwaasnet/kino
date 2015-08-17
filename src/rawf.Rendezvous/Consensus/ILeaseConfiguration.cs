using System;

namespace rawf.Rendezvous.Consensus
{
    public interface ILeaseConfiguration
    {
        TimeSpan MaxLeaseTimeSpan { get; }
        TimeSpan ClockDrift { get; }
        TimeSpan MessageRoundtrip { get; }
        TimeSpan NodeResponseTimeout { get; }
    }
}