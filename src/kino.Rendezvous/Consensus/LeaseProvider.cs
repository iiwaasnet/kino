﻿using System;
using System.Threading;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Framework;

namespace kino.Rendezvous.Consensus
{
    public partial class LeaseProvider : ILeaseProvider
    {
        private readonly IBallotGenerator ballotGenerator;
        private readonly LeaseConfiguration config;
        private readonly Timer leaseTimer;
        private readonly ILogger logger;
        private readonly Node localNode;
        private readonly IRoundBasedRegister register;
        private readonly SemaphoreSlim renewGateway;
        private volatile Lease lastKnownLease;
        private readonly TimeSpan leaseRenewWaitTimeout;
        private byte[] ownerPayload;

        public LeaseProvider(IRoundBasedRegister register,
                             IBallotGenerator ballotGenerator,
                             LeaseConfiguration config,
                             ISynodConfiguration synodConfig,
                             ILogger logger)
        {
            ValidateConfiguration(config);

            WaitBeforeNextLeaseIssued(config);

            localNode = synodConfig.LocalNode;
            this.logger = logger;
            this.config = config;
            this.ballotGenerator = ballotGenerator;
            this.register = register;
            leaseRenewWaitTimeout = TimeSpan.FromMilliseconds(10);
            renewGateway = new SemaphoreSlim(1);
            leaseTimer = new Timer(state => ScheduledReadOrRenewLease(), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
        }

        public Lease GetLease()
        {
            return GetLease(new byte[0]);
        }

        public Lease GetLease(byte[] ownerPayload)
        {
            Interlocked.Exchange(ref this.ownerPayload, ownerPayload);

            return GetLastKnownLease();
        }

        public void Dispose()
        {
            leaseTimer.Change(TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
            leaseTimer.Dispose();
            renewGateway.Dispose();
        }

        private void ValidateConfiguration(LeaseConfiguration config)
        {
            if (config.NodeResponseTimeout.TotalMilliseconds * 2 > config.MessageRoundtrip.TotalMilliseconds)
            {
                throw new Exception("NodeResponseTimeout[{config.NodeResponseTimeout.TotalMilliseconds} msec] " +
                                    "should be at least 2 times shorter than " +
                                    "MessageRoundtrip[{config.MessageRoundtrip.TotalMilliseconds} msec]");
            }
            if (config.MaxLeaseTimeSpan
                - TimeSpan.FromTicks(config.MessageRoundtrip.Ticks * 2)
                - config.ClockDrift <= TimeSpan.Zero)
            {
                throw new Exception($"MaxLeaseTimeSpan[{config.MaxLeaseTimeSpan.TotalMilliseconds} msec] " +
                                    "should be longer than " +
                                    $"(2 * MessageRoundtrip[{config.MessageRoundtrip.TotalMilliseconds} msec] " +
                                    $"+ ClockDrift[{config.ClockDrift.TotalMilliseconds} msec])");
            }
        }

        private void ScheduledReadOrRenewLease()
        {
            if (renewGateway.Wait(leaseRenewWaitTimeout))
            {
                try
                {
                    ReadOrRenewLease();
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
                finally
                {
                    renewGateway.Release();
                }
            }
        }

        private void ReadOrRenewLease()
        {
            var now = DateTime.UtcNow;

            var lease = AсquireOrLearnLease(ballotGenerator.New(localNode.SocketIdentity), now);

            if (ProcessBecameLeader(lease, lastKnownLease) || ProcessLostLeadership(lease, lastKnownLease))
            {
                var renewPeriod = CalcLeaseRenewPeriod(ProcessBecameLeader(lease, lastKnownLease));
                leaseTimer.Change(renewPeriod, renewPeriod);
            }

            lastKnownLease = lease;
        }

        private bool ProcessLostLeadership(Lease nextLease, Lease previousLease)
        {
            return (previousLease != null && Unsafe.Equals(previousLease.OwnerIdentity, localNode.SocketIdentity)
                    && nextLease != null && !Unsafe.Equals(nextLease.OwnerIdentity, localNode.SocketIdentity));
        }

        private bool ProcessBecameLeader(Lease nextLease, Lease previousLease)
        {
            return ((previousLease == null || !Unsafe.Equals(previousLease.OwnerIdentity, localNode.SocketIdentity))
                    && nextLease != null && Unsafe.Equals(nextLease.OwnerIdentity, localNode.SocketIdentity));
        }

        private TimeSpan CalcLeaseRenewPeriod(bool leader)
        {
            return (leader)
                       ? config.MaxLeaseTimeSpan
                         - TimeSpan.FromTicks(config.MessageRoundtrip.Ticks * 2)
                         - config.ClockDrift
                       : config.MaxLeaseTimeSpan;
        }

        private Lease GetLastKnownLease()
        {
            var now = DateTime.UtcNow;

            renewGateway.Wait();
            try
            {
                if (LeaseNullOrExpired(lastKnownLease, now))
                {
                    ReadOrRenewLease();
                }

                return lastKnownLease;
            }
            finally
            {
                renewGateway.Release();
            }
        }

        private Lease AсquireOrLearnLease(Ballot ballot, DateTime now)
        {
            var read = register.Read(ballot);
            if (read.TxOutcome == TxOutcome.Commit)
            {
                var lease = read.Lease;
                if (LeaseIsNotSafelyExpired(lease, now))
                {
                    LogStartSleep();
                    Sleep(config.ClockDrift);
                    LogAwake();

                    // TODO: Add recursion exit condition
                    return AсquireOrLearnLease(ballotGenerator.New(localNode.SocketIdentity), DateTime.UtcNow);
                }

                if (LeaseNullOrExpired(lease, now) || IsLeaseOwner(lease))
                {
                    LogLeaseProlonged(lease);
                    lease = new Lease(localNode.SocketIdentity, now + config.MaxLeaseTimeSpan, Interlocked.Exchange(ref ownerPayload, ownerPayload));
                }

                var write = register.Write(ballot, lease);
                if (write.TxOutcome == TxOutcome.Commit)
                {
                    return lease;
                }
            }

            return null;
        }

        private bool IsLeaseOwner(Lease lease)
        {
            return lease != null && Unsafe.Equals(lease.OwnerIdentity, localNode.SocketIdentity);
        }

        private static bool LeaseNullOrExpired(Lease lease, DateTime now)
        {
            return lease == null || lease.ExpiresAt < now;
        }

        private bool LeaseIsNotSafelyExpired(Lease lease, DateTime now)
        {
            return lease != null
                   && lease.ExpiresAt < now
                   && lease.ExpiresAt + config.ClockDrift > now;
        }

        private void WaitBeforeNextLeaseIssued(LeaseConfiguration config)
        {
            Sleep(config.MaxLeaseTimeSpan);
        }

        private void Sleep(TimeSpan delay)
        {
            using (var @lock = new ManualResetEvent(false))
            {
                @lock.WaitOne(delay);
            }
        }
    }
}