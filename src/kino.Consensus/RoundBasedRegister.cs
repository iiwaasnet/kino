using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Consensus.Configuration;
using kino.Consensus.Messages;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;

namespace kino.Consensus
{
    public partial class RoundBasedRegister : IRoundBasedRegister
    {
        private readonly IIntercomMessageHub intercomMessageHub;
        private Ballot readBallot;
        private Ballot writeBallot;
        private Lease lease;
        private readonly Listener listener;
        private readonly ISynodConfiguration synodConfig;
        private readonly LeaseConfiguration leaseConfig;
        private readonly ILogger logger;

        private readonly IObservable<IMessage> ackReadStream;
        private readonly IObservable<IMessage> nackReadStream;
        private readonly IObservable<IMessage> ackWriteStream;
        private readonly IObservable<IMessage> nackWriteStream;

        public RoundBasedRegister(IIntercomMessageHub intercomMessageHub,
                                  IBallotGenerator ballotGenerator,
                                  ISynodConfiguration synodConfig,
                                  LeaseConfiguration leaseConfig,
                                  ILogger logger)
        {
            this.logger = logger;
            this.synodConfig = synodConfig;
            this.leaseConfig = leaseConfig;
            this.intercomMessageHub = intercomMessageHub;
            readBallot = ballotGenerator.Null();
            writeBallot = ballotGenerator.Null();

            listener = intercomMessageHub.Subscribe();

            listener.Where(m => m.Equals(ConsensusMessages.LeaseRead))
                    .Subscribe(OnReadReceived);
            listener.Where(m => m.Equals(ConsensusMessages.LeaseWrite))
                    .Subscribe(OnWriteReceived);

            ackReadStream = listener.Where(m => m.Equals(ConsensusMessages.LeaseAckRead));
            nackReadStream = listener.Where(m => m.Equals(ConsensusMessages.LeaseNackRead));
            ackWriteStream = listener.Where(m => m.Equals(ConsensusMessages.LeaseAckWrite));
            nackWriteStream = listener.Where(m => m.Equals(ConsensusMessages.LeaseNackWrite));

            WaitBeforeNextLeaseIssued(leaseConfig);
        }

        private void WaitBeforeNextLeaseIssued(LeaseConfiguration config)
        {
            Task.Delay(config.MaxLeaseTimeSpan).ContinueWith(_ => intercomMessageHub.Start());
        }

        private void OnWriteReceived(IMessage message)
        {
            var payload = message.GetPayload<LeaseWriteMessage>();

            var ballot = new Ballot(new DateTime(payload.Ballot.Timestamp, DateTimeKind.Utc),
                                    payload.Ballot.MessageNumber,
                                    payload.Ballot.Identity);
            IMessage response;
            if (writeBallot > ballot || readBallot > ballot)
            {
                LogNackWrite(ballot);

                response = Message.Create(new LeaseNackWriteMessage
                                          {
                                              Ballot = payload.Ballot,
                                              SenderUri = synodConfig.LocalNode.Uri.ToSocketAddress()
                                          });
            }
            else
            {
                LogAckWrite(ballot);

                writeBallot = ballot;
                lease = new Lease(payload.Lease.Identity, new DateTime(payload.Lease.ExpiresAt, DateTimeKind.Utc), payload.Lease.OwnerPayload);

                response = Message.Create(new LeaseAckWriteMessage
                                          {
                                              Ballot = payload.Ballot,
                                              SenderUri = synodConfig.LocalNode.Uri.ToSocketAddress()
                                          });
            }
            intercomMessageHub.Send(response, payload.SenderIdentity);
        }

        private void OnReadReceived(IMessage message)
        {
            var payload = message.GetPayload<LeaseReadMessage>();

            var ballot = new Ballot(new DateTime(payload.Ballot.Timestamp, DateTimeKind.Utc),
                                    payload.Ballot.MessageNumber,
                                    payload.Ballot.Identity);

            IMessage response;
            if (writeBallot >= ballot || readBallot >= ballot)
            {
                LogNackRead(ballot);

                response = Message.Create(new LeaseNackReadMessage
                                          {
                                              Ballot = payload.Ballot,
                                              SenderUri = synodConfig.LocalNode.Uri.ToSocketAddress()
                                          });
            }
            else
            {
                LogAckRead(ballot);

                readBallot = ballot;

                response = CreateLeaseAckReadMessage(payload);
            }

            intercomMessageHub.Send(response, payload.SenderIdentity);
        }

        public LeaseTxResult Read(Ballot ballot)
        {
            var ackFilter = new LeaderElectionMessageFilter(ballot, m => m.GetPayload<LeaseAckReadMessage>(), synodConfig);
            var nackFilter = new LeaderElectionMessageFilter(ballot, m => m.GetPayload<LeaseNackReadMessage>(), synodConfig);

            var awaitableAckFilter = new AwaitableMessageStreamFilter(ackFilter.Match, m => m.GetPayload<LeaseAckReadMessage>(), GetQuorum());
            var awaitableNackFilter = new AwaitableMessageStreamFilter(nackFilter.Match, m => m.GetPayload<LeaseNackReadMessage>(), GetQuorum());

            using (ackReadStream.Subscribe(awaitableAckFilter))
            {
                using (nackReadStream.Subscribe(awaitableNackFilter))
                {
                    var message = CreateReadMessage(ballot);
                    intercomMessageHub.Broadcast(message);

                    var index = WaitHandle.WaitAny(new[] {awaitableAckFilter.Filtered, awaitableNackFilter.Filtered},
                                                   leaseConfig.NodeResponseTimeout);

                    if (ReadNotAcknowledged(index))
                    {
                        return new LeaseTxResult {TxOutcome = TxOutcome.Abort};
                    }

                    var lease = awaitableAckFilter
                        .MessageStream
                        .Select(m => m.GetPayload<LeaseAckReadMessage>())
                        .Max(p => CreateLastWrittenLease(p))
                        .Lease;

                    return new LeaseTxResult
                           {
                               TxOutcome = TxOutcome.Commit,
                               Lease = lease
                           };
                }
            }
        }

        private static LastWrittenLease CreateLastWrittenLease(LeaseAckReadMessage p)
        {
            return new LastWrittenLease(new Ballot(p.KnownWriteBallot.Timestamp, p.KnownWriteBallot.MessageNumber, p.KnownWriteBallot.Identity),
                                        (p.Lease != null)
                                            ? new Lease(p.Lease.Identity,
                                                        p.Lease.ExpiresAt,
                                                        p.Lease.OwnerPayload)
                                            : null);
        }

        public LeaseTxResult Write(Ballot ballot, Lease lease)
        {
            var ackFilter = new LeaderElectionMessageFilter(ballot, m => m.GetPayload<LeaseAckWriteMessage>(), synodConfig);
            var nackFilter = new LeaderElectionMessageFilter(ballot, m => m.GetPayload<LeaseNackWriteMessage>(), synodConfig);

            var awaitableAckFilter = new AwaitableMessageStreamFilter(ackFilter.Match, m => m.GetPayload<LeaseAckWriteMessage>(), GetQuorum());
            var awaitableNackFilter = new AwaitableMessageStreamFilter(nackFilter.Match, m => m.GetPayload<LeaseNackWriteMessage>(), GetQuorum());

            using (ackWriteStream.Subscribe(awaitableAckFilter))
            {
                using (nackWriteStream.Subscribe(awaitableNackFilter))
                {
                    intercomMessageHub.Broadcast(CreateWriteMessage(ballot, lease));

                    var index = WaitHandle.WaitAny(new[] {awaitableAckFilter.Filtered, awaitableNackFilter.Filtered},
                                                   leaseConfig.NodeResponseTimeout);

                    if (ReadNotAcknowledged(index))
                    {
                        return new LeaseTxResult {TxOutcome = TxOutcome.Abort};
                    }

                    return new LeaseTxResult
                           {
                               TxOutcome = TxOutcome.Commit,
                               // NOTE: needed???
                               Lease = lease
                           };
                }
            }
        }

        private static bool ReadNotAcknowledged(int index)
        {
            return index == 1 || index == WaitHandle.WaitTimeout;
        }

        private int GetQuorum()
        {
            return synodConfig.Synod.Count() / 2 + 1;
        }

        public void Dispose()
        {
            intercomMessageHub.Stop();
            listener.Dispose();
        }

        private IMessage CreateWriteMessage(Ballot ballot, Lease lease)
        {
            return Message.Create(new LeaseWriteMessage
                                  {
                                      Ballot = new Messages.Ballot
                                               {
                                                   Identity = ballot.Identity,
                                                   Timestamp = ballot.Timestamp.Ticks,
                                                   MessageNumber = ballot.MessageNumber
                                               },
                                      Lease = new Messages.Lease
                                              {
                                                  Identity = lease.OwnerIdentity,
                                                  ExpiresAt = lease.ExpiresAt.Ticks,
                                                  OwnerPayload = lease.OwnerPayload
                                      }
                                  });
        }

        private IMessage CreateReadMessage(Ballot ballot)
        {
            return Message.Create(new LeaseReadMessage
                                  {
                                      Ballot = new Messages.Ballot
                                               {
                                                   Identity = ballot.Identity,
                                                   Timestamp = ballot.Timestamp.Ticks,
                                                   MessageNumber = ballot.MessageNumber
                                               }
                                  });
        }

        private IMessage CreateLeaseAckReadMessage(LeaseReadMessage payload)
        {
            return Message.Create(new LeaseAckReadMessage
                                  {
                                      Ballot = payload.Ballot,
                                      KnownWriteBallot = new Messages.Ballot
                                                         {
                                                             Identity = writeBallot.Identity,
                                                             Timestamp = writeBallot.Timestamp.Ticks,
                                                             MessageNumber = writeBallot.MessageNumber
                                                         },
                                      Lease = (lease != null)
                                                  ? new Messages.Lease
                                                    {
                                                        Identity = lease.OwnerIdentity,
                                                        ExpiresAt = lease.ExpiresAt.Ticks,
                                                        OwnerPayload = lease.OwnerPayload
                                                    }
                                                  : null,
                                      SenderUri = synodConfig.LocalNode.Uri.ToSocketAddress()
                                  });
        }
    }
}