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

        
        public void Setup()
        {
            ownerPayload = Guid.NewGuid().ToByteArray();
        }

        [Fact]
        public void ReadCommitsWithNonEmptyLease_IfWriteCommittedLeaseWithBallotLessThanCurrent()
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
                        Assert.Equal(TxOutcome.Commit, txResult.TxOutcome);

                        var ballot1 = ballotGenerator.New(localNode.SocketIdentity);
                        Assert.True(ballot0 < ballot1);
                        txResult = roundBasedRegister.Read(ballot1);

                        Assert.Equal(TxOutcome.Commit, txResult.TxOutcome);
                        Assert.Equal(lease.ExpiresAt, txResult.Lease.ExpiresAt);
                        CollectionAssert.Equal(lease.OwnerPayload, txResult.Lease.OwnerPayload);
                        Assert.True(Unsafe.ArraysEqual(lease.OwnerIdentity, txResult.Lease.OwnerIdentity));
                    }
                }
            }
        }
    }
}