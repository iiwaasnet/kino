using System;
using rawf.Consensus.Messages;
using rawf.Framework;
using rawf.Messaging;

namespace rawf.Consensus
{
    public class LeaderElectionMessageFilter
    {
        private readonly IBallot ballot;
        private readonly Func<IMessage, ILeaseMessage> payload;
        private readonly ISynodConfiguration synodConfig;

        public LeaderElectionMessageFilter(IBallot ballot,
                                           Func<IMessage, ILeaseMessage> payload,
                                           ISynodConfiguration synodConfig)
        {
            this.ballot = ballot;
            this.synodConfig = synodConfig;
            this.payload = payload;
        }

        public bool Match(IMessage message)
        {
            var messagePayload = payload(message);

            return synodConfig.BelongsToSynod(new Uri(messagePayload.SenderUri))
                   && Unsafe.Equals(messagePayload.Ballot.Identity, ballot.Identity)
                   && messagePayload.Ballot.Timestamp == ballot.Timestamp.Ticks
                   && messagePayload.Ballot.MessageNumber == ballot.MessageNumber;
        }
    }
}