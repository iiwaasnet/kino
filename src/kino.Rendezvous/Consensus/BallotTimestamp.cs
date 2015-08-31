using System;

namespace kino.Rendezvous.Consensus
{
    internal class BallotTimestamp
    {
        internal DateTime Timestamp { get; set; }

        internal int MessageNumber { get; set; }
    }
}