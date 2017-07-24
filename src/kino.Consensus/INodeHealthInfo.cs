using System;

namespace kino.Consensus
{
    public interface INodeHealthInfo
    {
        bool IsHealthy();

        DateTime LastKnownHeartBeat { get; }

        Uri NodeUri { get; }
    }
}