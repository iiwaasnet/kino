using System;
using kino.Consensus.Configuration;
using kino.Consensus.Messages;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Consensus
{
    public class LeaderElectionMessageFilter
    {
        private readonly Ballot ballot;
        private readonly Func<IMessage, ILeaseMessage> payload;
        private readonly ISynodConfigurationProvider synodConfigProvider;

        public LeaderElectionMessageFilter(Ballot ballot,
                                           Func<IMessage, ILeaseMessage> payload,
                                           ISynodConfigurationProvider synodConfigProvider)
        {
            this.ballot = ballot;
            this.synodConfigProvider = synodConfigProvider;
            this.payload = payload;
        }

        public bool Match(IMessage message)
        {
            var messagePayload = payload(message);

            return synodConfigProvider.BelongsToSynod(messagePayload.SenderUri)
                   && Unsafe.ArraysEqual(messagePayload.Ballot.Identity, ballot.Identity)
                   && messagePayload.Ballot.Timestamp == ballot.Timestamp.Ticks
                   && messagePayload.Ballot.MessageNumber == ballot.MessageNumber;
        }
    }
}