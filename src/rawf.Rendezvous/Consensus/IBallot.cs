using System;

namespace rawf.Rendezvous.Consensus
{
    public interface IBallot : IComparable
    {
        byte[] Identity { get; }
        DateTime Timestamp { get; }
        int MessageNumber { get; }
    }
}