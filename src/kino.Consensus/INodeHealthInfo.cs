using System;

namespace kino.Consensus
{
    public interface INodeHealthInfo
    {
        bool IsHealthy();

        DateTime LastKnownHeartBeat { get; }

        DateTime LastReconnectAttempt { get; }

        string NodeUri { get; }
    }
}