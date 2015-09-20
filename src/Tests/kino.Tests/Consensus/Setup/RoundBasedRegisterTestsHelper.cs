using System;
using System.Collections.Generic;
using System.Threading;
using kino.Diagnostics;
using kino.Rendezvous.Configuration;
using kino.Rendezvous.Consensus;
using kino.Sockets;
using kino.Tests.Helpers;
using Moq;
using SynodConfiguration = kino.Rendezvous.Configuration.SynodConfiguration;

namespace kino.Tests.Consensus.Setup
{
    public class RoundBasedRegisterTestsHelper
    {
        internal static RoundBasedRegisterTestSetup CreateRoundBasedRegister(IEnumerable<string> synod, string localNodeUri)
        {
            var intercomMessageHubSocketFactory = new IntercomMessageHubSocketFactory();
            var socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(intercomMessageHubSocketFactory.CreateSubscriberSocket);
            var appConfig = new ApplicationConfiguration
                            {
                                Synod = new SynodConfiguration
                                        {
                                            Members = synod,
                                            ClockDrift = TimeSpan.FromMilliseconds(100),
                                            MessageRoundtrip = TimeSpan.FromSeconds(4),
                                            NodeResponseTimeout = TimeSpan.FromSeconds(2),
                                            LocalNode = localNodeUri,
                                            MaxLeaseTimeSpan = TimeSpan.FromSeconds(10)
                                        }
                            };
            var leaseConfig = new LeaseConfiguration
                              {
                                  ClockDrift = appConfig.Synod.ClockDrift,
                                  MaxLeaseTimeSpan = appConfig.Synod.MaxLeaseTimeSpan,
                                  MessageRoundtrip = appConfig.Synod.MessageRoundtrip,
                                  NodeResponseTimeout = appConfig.Synod.NodeResponseTimeout
                              };
            var socketConfig = new SocketConfiguration
                               {
                                   ReceivingHighWatermark = 1000,
                                   SendingHighWatermark = 1000,
                                   Linger = TimeSpan.Zero
                               };
            var synodConfig = new Rendezvous.Consensus.SynodConfiguration(new SynodConfigurationProvider(appConfig));
            var loggerMock = new Mock<ILogger>();
            var intercomMessageHub = new IntercomMessageHub(new SocketFactory(socketConfig),
                                                            synodConfig,
                                                            loggerMock.Object);
            var ballotGenerator = new BallotGenerator(leaseConfig);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodConfig,
                                                            leaseConfig,
                                                            loggerMock.Object);

            Thread.Sleep(TimeSpan.FromMilliseconds(400));

            return new RoundBasedRegisterTestSetup(ballotGenerator, synodConfig.LocalNode, roundBasedRegister);
        }

        internal static string[] GetSynodMembers()
        {
            return new[]
                   {
                       "tcp://127.0.0.1:3001",
                       "tcp://127.0.0.2:3002",
                       "tcp://127.0.0.3:3003"
                   };
        }
    }
}