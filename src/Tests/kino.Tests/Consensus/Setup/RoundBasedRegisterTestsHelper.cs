using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Rendezvous.Configuration;
using Moq;

namespace kino.Tests.Consensus.Setup
{
    public static class RoundBasedRegisterTestsHelper
    {
        private static readonly TimeSpan WaitTime = TimeSpan.FromMilliseconds(500);
        private static int portNumber = 100;

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
            var logger = new Mock<ILogger>();
            var performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            var synodConfigProvider = new SynodConfigurationProvider(appConfig.Synod);
            var intercomMessageHub = new IntercomMessageHub(new SocketFactory(socketConfig),
                                                            synodConfigProvider,
                                                            performanceCounterManager.Object,
                                                            logger.Object);
            var ballotGenerator = new BallotGenerator(appConfig.Lease);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodConfigProvider,
                                                            appConfig.Lease,
                                                            logger.Object);

            logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);

            return new RoundBasedRegisterTestSetup(ballotGenerator,
                                                   synodConfigProvider.LocalNode,
                                                   roundBasedRegister);
        }

        internal static LeaseTxResult RepeatUntil(Func<LeaseTxResult> func, TxOutcome expected)
        {
            var repeat = 10;

            LeaseTxResult result;
            while ((result = func()).TxOutcome != expected && repeat-- > 0)
            {
                WaitTime.Sleep();
            }
            return result;
        }

        internal static IEnumerable<string> GetSynodMembers()
        {
            return GetSynodMembers().ToList();

            IEnumerable<string> GetSynodMembers()
            {
                yield return $"tcp://127.0.0.22:{portNumber++}";
                yield return $"tcp://127.0.0.22:{portNumber++}";
                yield return $"tcp://127.0.0.22:{portNumber++}";
            }

            //yield return "tcp://127.0.0.2:3001";
            //yield return "tcp://127.0.0.2:3002";
            //yield return "tcp://127.0.0.2:3003";
        }
    }
}