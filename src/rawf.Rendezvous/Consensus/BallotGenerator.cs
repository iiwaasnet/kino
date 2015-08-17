using System;

namespace rawf.Rendezvous.Consensus
{
    public class BallotGenerator : IBallotGenerator
    {
        private readonly BallotTimestamp lastBallotTimestamp;
        private readonly ILeaseConfiguration config;
        private static readonly byte[] Empty = new byte[0];
        private readonly Ballot NullBallot;

        public BallotGenerator(ILeaseConfiguration config)
        {
            this.config = config;
            lastBallotTimestamp = new BallotTimestamp {MessageNumber = 0, Timestamp = DateTime.UtcNow};
            NullBallot = new Ballot(DateTime.MinValue, 0, Empty);
        }

        public IBallot New(byte[] identity)
        {
            var now = DateTime.UtcNow;

            if (now >= lastBallotTimestamp.Timestamp
                || now <= lastBallotTimestamp.Timestamp + config.ClockDrift)
            {
                lastBallotTimestamp.MessageNumber = ++lastBallotTimestamp.MessageNumber;
            }
            else
            {
                lastBallotTimestamp.MessageNumber = 0;
            }

            lastBallotTimestamp.Timestamp = now;

            return new Ballot(lastBallotTimestamp.Timestamp, lastBallotTimestamp.MessageNumber, identity);
        }

        public IBallot Null()
            => NullBallot;
    }
}