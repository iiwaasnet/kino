using System;
using System.Collections.Generic;
using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Rendezvous.Configuration;
using Moq;
using SynodConfiguration = kino.Rendezvous.Configuration.SynodConfiguration;

namespace kino.Tests.Consensus.Setup
{
    public static class RoundBasedRegisterTestsHelper
    {
        internal static RoundBasedRegisterTestSetup CreateRoundBasedRegister(IEnumerable<string> synod, string localNodeUri)
        {
            var appConfig = new RendezvousServiceConfiguration
                            {
                                Synod = new SynodConfiguration
                                        {
                                            Members = synod,
                                            LocalNode = localNodeUri,
                                            HeartBeatInterval = TimeSpan.FromSeconds(5),
                                            IntercomEndpoint = $"inproc://{Guid.NewGuid()}",
                                            MissingHeartBeatsBeforeReconnect = 4
                                        },
                                Lease = new LeaseConfiguration
                                        {
                                            ClockDrift = TimeSpan.FromMilliseconds(10),
                                            MessageRoundtrip = TimeSpan.FromMilliseconds(100),
                                            NodeResponseTimeout = TimeSpan.FromMilliseconds(1000),
                                            MaxLeaseTimeSpan = TimeSpan.FromSeconds(3)
                                        }
                            };

            var socketConfig = new SocketConfiguration
                               {
                                   ReceivingHighWatermark = 1000,
                                   SendingHighWatermark = 1000,
                                   Linger = TimeSpan.Zero
                               };
            var synodConfig = new global::kino.Consensus.Configuration.SynodConfiguration(new SynodConfigurationProvider(appConfig.Synod));
            var logger = new Mock<ILogger>();
            var performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            var intercomMessageHub = new IntercomMessageHub(new SocketFactory(socketConfig),
                                                            synodConfig,
                                                            performanceCounterManager.Object,
                                                            logger.Object);
            var ballotGenerator = new BallotGenerator(appConfig.Lease);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodConfig,
                                                            appConfig.Lease,
                                                            logger.Object);

            return new RoundBasedRegisterTestSetup(ballotGenerator,
                                                   synodConfig.LocalNode,
                                                   roundBasedRegister);
        }

        internal static LeaseTxResult RepeatUntil(Func<LeaseTxResult> func, TxOutcome expected)
        {
            var repeat = 3;

            LeaseTxResult result;
            while ((result = func()).TxOutcome != expected && repeat-- > 0)
                ;

            return result;
        }

        internal static IEnumerable<string> GetSynodMembers()
        {
            yield return "tcp://127.0.0.1:3001";
            yield return "tcp://127.0.0.1:3002";
            yield return "tcp://127.0.0.1:3003";
        }
    }
}