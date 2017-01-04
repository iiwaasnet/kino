using System;
using kino.Consensus;
using kino.Core;
using kino.Core.Framework;

namespace kino.Tests.Consensus.Setup
{
    internal class RoundBasedRegisterTestSetup : IDisposable
    {
        private readonly TimeSpan maxLeaseTimeSpan;

        public RoundBasedRegisterTestSetup(IBallotGenerator ballotGenerator,
                                           Node localNode,
                                           IRoundBasedRegister roundBasedRegister,
                                           TimeSpan maxLeaseTimeSpan)
        {
            this.maxLeaseTimeSpan = maxLeaseTimeSpan;
            BallotGenerator = ballotGenerator;
            LocalNode = localNode;
            RoundBasedRegister = roundBasedRegister;
        }

        public IBallotGenerator BallotGenerator { get; }

        public Node LocalNode { get; }

        public IRoundBasedRegister RoundBasedRegister { get; }

        public void Dispose()
            => RoundBasedRegister?.Dispose();

        public void WaitUntilStarted()
            => (maxLeaseTimeSpan + TimeSpan.FromMilliseconds(1000)).Sleep();
    }
}