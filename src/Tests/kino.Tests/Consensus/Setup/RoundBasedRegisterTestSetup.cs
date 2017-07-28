using System;
using kino.Consensus;
using kino.Core;

namespace kino.Tests.Consensus.Setup
{
    internal class RoundBasedRegisterTestSetup : IDisposable
    {
        public RoundBasedRegisterTestSetup(IBallotGenerator ballotGenerator,
                                           Node localNode,
                                           IRoundBasedRegister roundBasedRegister)
        {
            BallotGenerator = ballotGenerator;
            LocalNode = localNode;
            RoundBasedRegister = roundBasedRegister;
        }

        public IBallotGenerator BallotGenerator { get; }

        public Node LocalNode { get; }

        public IRoundBasedRegister RoundBasedRegister { get; }

        public void Dispose()
            => RoundBasedRegister?.Dispose();
    }
}