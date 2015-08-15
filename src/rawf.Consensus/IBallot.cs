using System;

namespace rawf.Consensus
{
    public interface IBallot : IComparable
    {
        byte[] Identity { get; }
        DateTime Timestamp { get; }
        int MessageNumber { get; }
    }
}