using System;
using System.Linq;
using kino.Consensus;
using kino.Core.Framework;
using NUnit.Framework;
using static kino.Tests.Consensus.Setup.RoundBasedRegisterTestsHelper;

namespace kino.Tests.Consensus
{
    [TestFixture(Category = "FLease",
        Description = @"Lemma R1: Read-abort: If READ(k) aborts, then some
operation READ(k0) or WRITE(k0; *) was invoked with k0 >= k.")]
    public class RoundBasedRegisterTests_LemmaR1
    {
        private byte[] ownerPayload;

        [SetUp]
        public void Setup()
        {
            ownerPayload = Guid.NewGuid().ToByteArray();
        }

        [Test]
        public void ReadIsAborted_AfterReadWithBallotEqualToCurrent()
        {
            using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().First()))
            {
                using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Second()))
                {
                    using (var testSetup = CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Third()))
                    {
                        var ballotGenerator = testSetup.BallotGenerator;
                        var localNode = testSetup.LocalNode;
                        var roundBasedRegister = testSetup.RoundBasedRegister;

                        var ballot0 = ballotGenerator.New(localNode.SocketIdentity);
                        var txResult = RepeatUntil(() => roundBasedRegister.Read(ballot0), TxOutcome.Commit);
                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);

                        txResult = roundBasedRegister.Read(ballot0);
                        Assert.AreEqual(TxOutcome.Abort, txResult.TxOutcome);
                    }
                }
            }
        }

        [Test]
        public void ReadIsAborted_AfterReadWithBallotGreaterThanCurrent()
        {
            using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().First()))
            {
                using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Second()))
                {
                    using (var testSetup = CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Third()))
                    {
                        var ballotGenerator = testSetup.BallotGenerator;
                        var localNode = testSetup.LocalNode;
                        var roundBasedRegister = testSetup.RoundBasedRegister;

                        var ballot0 = ballotGenerator.New(localNode.SocketIdentity);
                        var txResult = RepeatUntil(() => roundBasedRegister.Read(ballot0), TxOutcome.Commit);
                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);

                        var ballot1 = new Ballot(ballot0.Timestamp - TimeSpan.FromSeconds(10), ballot0.MessageNumber, localNode.SocketIdentity);
                        Assert.IsTrue(ballot0 > ballot1);
                        txResult = roundBasedRegister.Read(ballot1);
                        Assert.AreEqual(TxOutcome.Abort, txResult.TxOutcome);
                    }
                }
            }
        }

        [Test]
        public void ReadIsAborted_AfterWriteWithBallotEqualToCurrent()
        {
            using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().First()))
            {
                using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Second()))
                {
                    using (var testSetup = CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Third()))
                    {
                        var ballotGenerator = testSetup.BallotGenerator;
                        var localNode = testSetup.LocalNode;
                        var roundBasedRegister = testSetup.RoundBasedRegister;

                        var ballot0 = ballotGenerator.New(localNode.SocketIdentity);
                        var lease = new Lease(localNode.SocketIdentity, DateTime.UtcNow, ownerPayload);
                        var txResult = RepeatUntil(() => roundBasedRegister.Write(ballot0, lease), TxOutcome.Commit);
                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);

                        txResult = roundBasedRegister.Read(ballot0);
                        Assert.AreEqual(TxOutcome.Abort, txResult.TxOutcome);
                    }
                }
            }
        }

        [Test]
        public void ReadIsAborted_AfterWriteWithBallotGreaterThanCurrent()
        {
            using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().First()))
            {
                using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Second()))
                {
                    using (var testSetup = CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Third()))
                    {
                        var ballotGenerator = testSetup.BallotGenerator;
                        var localNode = testSetup.LocalNode;
                        var roundBasedRegister = testSetup.RoundBasedRegister;

                        var ballot0 = ballotGenerator.New(localNode.SocketIdentity);
                        var lease = new Lease(localNode.SocketIdentity, DateTime.UtcNow, ownerPayload);
                        var txResult = RepeatUntil(() => roundBasedRegister.Write(ballot0, lease), TxOutcome.Commit);
                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);

                        var ballot1 = new Ballot(ballot0.Timestamp - TimeSpan.FromSeconds(10), ballot0.MessageNumber, localNode.SocketIdentity);
                        Assert.IsTrue(ballot0 > ballot1);
                        txResult = roundBasedRegister.Read(ballot1);
                        Assert.AreEqual(TxOutcome.Abort, txResult.TxOutcome);
                    }
                }
            }
        }
    }
}