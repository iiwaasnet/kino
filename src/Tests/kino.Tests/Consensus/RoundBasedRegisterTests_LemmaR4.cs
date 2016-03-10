using System;
using System.Linq;
using kino.Consensus;
using kino.Core.Framework;
using NUnit.Framework;
using static kino.Tests.Consensus.Setup.RoundBasedRegisterTestsHelper;

namespace kino.Tests.Consensus
{
    [TestFixture(Category = "FLease", Description = @"Lemma R4: Read-commit: If READ(k) commits with v
and v != null, then some operation WRITE(k0; v) was invoked with k0 < k.")]
    public class RoundBasedRegisterTests_LemmaR4
    {
        private byte[] ownerPayload;

        [SetUp]
        public void Setup()
        {
            ownerPayload = Guid.NewGuid().ToByteArray();
        }

        [Test]
        public void TestReadCommitsWithNonEmptyLease_IfWriteCommittedLeaseWithBallotLessThanCurrent()
        {
            using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().First()))
            {
                using (CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Second()))
                {
                    using (var testSetup = CreateRoundBasedRegister(GetSynodMembers(), GetSynodMembers().Third()))
                    {
                        testSetup.WaitUntilStarted();

                        var ballotGenerator = testSetup.BallotGenerator;
                        var localNode = testSetup.LocalNode;
                        var roundBasedRegister = testSetup.RoundBasedRegister;

                        var ballot0 = ballotGenerator.New(localNode.SocketIdentity);
                        var lease = new Lease(localNode.SocketIdentity, DateTime.UtcNow, ownerPayload);
                        var txResult = roundBasedRegister.Write(ballot0, lease);
                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);

                        var ballot1 = ballotGenerator.New(localNode.SocketIdentity);
                        Assert.IsTrue(ballot0 < ballot1);
                        txResult = roundBasedRegister.Read(ballot1);

                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);
                        Assert.AreEqual(lease.ExpiresAt, txResult.Lease.ExpiresAt);
                        CollectionAssert.AreEqual(lease.OwnerPayload, txResult.Lease.OwnerPayload);
                        Assert.IsTrue(Unsafe.Equals(lease.OwnerIdentity, txResult.Lease.OwnerIdentity));
                    }
                }
            }
        }
    }
}