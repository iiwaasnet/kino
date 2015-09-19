using System;
using kino.Rendezvous.Consensus;
using NUnit.Framework;

namespace kino.Tests.Consensus
{
    [TestFixture]
    public class BallotGeneratorTests
    {
        [Test(Description = @"Extend k = (t; r; idp) to include an additional
message number r that is used to distinguish the messages
sent by a process within the same interval. r must only
be unique within an interval.")]
        public void TestTwoBallotsGeneratedWithinSafetyPeriod_HaveDifferentMessageNumber()
        {
            var identity = new byte[] {0};
            var leaseConfig = new LeaseConfiguration
                              {
                                  ClockDrift = TimeSpan.FromMilliseconds(500)
                              };
            var ballotGenerator = new BallotGenerator(leaseConfig);
            var ballot1 = ballotGenerator.New(identity);
            var ballot2 = ballotGenerator.New(identity);

            Assert.GreaterOrEqual(leaseConfig.ClockDrift, ballot2.Timestamp - ballot1.Timestamp);
            Assert.AreNotEqual(ballot1.MessageNumber, ballot2.MessageNumber);
        }
    }
}