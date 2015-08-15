using System;

namespace rawf.Consensus
{
    internal class BallotTimestamp
    {
        internal DateTime Timestamp { get; set; }

        internal int MessageNumber { get; set; }
    }
}