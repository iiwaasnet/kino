using System;
using System.Linq;
using kino.Framework;
using kino.Rendezvous.Consensus;
using NUnit.Framework;
using static kino.Tests.Consensus.Setup.RoundBasedRegisterTestsHelper;

namespace kino.Tests.Consensus
{
    [TestFixture(Category = "FLease", Description = @"Lemma R4: Read-commit: If READ(k) commits with v
and v != null, then some operation WRITE(k0; v) was invoked with k0 < k.")]
    public class RoundBasedRegisterTests_LemmaR4
    {
        private OwnerEndpoint ownerEndpoint;

        [SetUp]
        public void Setup()
        {
            ownerEndpoint = new OwnerEndpoint
                            {
                                MulticastUri = new Uri("tcp://127.0.0.1"),
                                UnicastUri = new Uri("tcp://127.0.0.1")
                            };
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
                        var ballotGenerator = testSetup.BallotGenerator;
                        var localNode = testSetup.LocalNode;
                        var roundBasedRegister = testSetup.RoundBasedRegister;

                        var ballot0 = ballotGenerator.New(localNode.SocketIdentity);
                        var lease = new Lease(localNode.SocketIdentity, ownerEndpoint, DateTime.UtcNow);
                        var txResult = roundBasedRegister.Write(ballot0, lease);
                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);

                        var ballot1 = ballotGenerator.New(localNode.SocketIdentity);
                        Assert.IsTrue(ballot0 < ballot1);
                        txResult = roundBasedRegister.Read(ballot1);

                        Assert.AreEqual(TxOutcome.Commit, txResult.TxOutcome);
                        Assert.AreEqual(lease.ExpiresAt, txResult.Lease.ExpiresAt);
                        Assert.AreEqual(lease.OwnerEndpoint.MulticastUri, txResult.Lease.OwnerEndpoint.MulticastUri);
                        Assert.AreEqual(lease.OwnerEndpoint.UnicastUri, txResult.Lease.OwnerEndpoint.UnicastUri);
                        Assert.IsTrue(Unsafe.Equals(lease.OwnerIdentity, txResult.Lease.OwnerIdentity));
                    }
                }
            }
        }
    }
}