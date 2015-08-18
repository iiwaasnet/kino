using System;
using System.Collections;

namespace rawf.Rendezvous.Consensus
{
    public class Ballot : IComparable
    {
        public Ballot(DateTime timestamp, int messageNumber, byte[] identity)
        {
            Timestamp = timestamp;
            MessageNumber = messageNumber;
            Identity = identity;
        }

        public Ballot(long timeStamp, int messageNumber, byte[] identity)
            :this(new DateTime(timeStamp, DateTimeKind.Utc), messageNumber, identity)
        {
        }

        public static bool operator <=(Ballot x, Ballot y)
        {
            var res = x.CompareTo(y);

            return res < 0 || res == 0;
        }

        public static bool operator >=(Ballot x, Ballot y)
        {
            var res = x.CompareTo(y);

            return res > 0 || res == 0;
        }

        public static bool operator <(Ballot x, Ballot y)
        {
            return x.CompareTo(y) < 0;
        }

        public static bool operator >(Ballot x, Ballot y)
        {
            return x.CompareTo(y) > 0;
        }

        public int CompareTo(object obj)
        {
            var ballot = obj as Ballot;

            var res = Timestamp.CompareTo(ballot.Timestamp);
            if (res != 0)
            {
                return res;
            }

            res = MessageNumber.CompareTo(ballot.MessageNumber);
            if (res != 0)
            {
                return res;
            }

            return StructuralComparisons.StructuralComparer.Compare(Identity, ballot.Identity);
        }

        public byte[] Identity { get; }
        public DateTime Timestamp { get; }
        public int MessageNumber { get; }
    }
}