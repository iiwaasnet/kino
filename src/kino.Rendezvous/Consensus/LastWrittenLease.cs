using System;

namespace kino.Rendezvous.Consensus
{
    public class LastWrittenLease : IComparable<LastWrittenLease>
    {
        private readonly Ballot writeBallot;

        public LastWrittenLease(Ballot writeBallot, Lease lease)
        {
            this.writeBallot = writeBallot;
            Lease = lease;
        }

        public int CompareTo(LastWrittenLease other)
        {
            return writeBallot.CompareTo(other.writeBallot);
        }

        public Lease Lease { get; }
    }
}