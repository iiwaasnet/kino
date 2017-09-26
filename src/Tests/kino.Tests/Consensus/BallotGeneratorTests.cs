using System;
using System.Threading;
using kino.Consensus;
using kino.Consensus.Configuration;
using Xunit;

namespace kino.Tests.Consensus
{
    public class BallotGeneratorTests
    {
        [Fact(DisplayName = @"Extend k = (t; r; idp) to include an additional
message number r that is used to distinguish the messages
sent by a process within the same interval. r must only
be unique within an interval.")]
        public void TwoBallotsGeneratedWithinSafetyPeriod_HaveDifferentMessageNumber()
        {
            var identity = new byte[] {0};
            var leaseConfig = new LeaseConfiguration
                              {
                                  ClockDrift = TimeSpan.FromMilliseconds(500)
                              };
            var ballotGenerator = new BallotGenerator(leaseConfig);
            var ballot1 = ballotGenerator.New(identity);
            Thread.Sleep((int) leaseConfig.ClockDrift.TotalMilliseconds / 10);
            var ballot2 = ballotGenerator.New(identity);

            Assert.InRange(leaseConfig.ClockDrift, ballot2.Timestamp - ballot1.Timestamp, TimeSpan.MaxValue);
            Assert.NotEqual(ballot1.MessageNumber, ballot2.MessageNumber);
        }
    }
}