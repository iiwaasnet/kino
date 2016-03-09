using System;
using System.Collections.Generic;
using System.Threading;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Sockets;
using kino.Rendezvous.Configuration;
using kino.Tests.Helpers;
using Moq;
using SynodConfiguration = kino.Rendezvous.Configuration.SynodConfiguration;

namespace kino.Tests.Consensus.Setup
{
    public class RoundBasedRegisterTestsHelper
    {
        internal static RoundBasedRegisterTestSetup CreateRoundBasedRegister(IEnumerable<Uri> synod, Uri localNodeUri)
        {
            var intercomMessageHubSocketFactory = new IntercomMessageHubSocketFactory();
            var socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(intercomMessageHubSocketFactory.CreateSubscriberSocket);
            var appConfig = new ApplicationConfiguration
                            {
                                Synod = new SynodConfiguration
                                        {
                                            Members = synod,
                                            LocalNode = localNodeUri
                                        },
                                Lease = new LeaseConfiguration
                                        {
                                            ClockDrift = TimeSpan.FromMilliseconds(50),
                                            MessageRoundtrip = TimeSpan.FromMilliseconds(100),
                                            NodeResponseTimeout = TimeSpan.FromMilliseconds(400),
                                            MaxLeaseTimeSpan = TimeSpan.FromSeconds(3)
                                        }
                            };

            var socketConfig = new SocketConfiguration
                               {
                                   ReceivingHighWatermark = 1000,
                                   SendingHighWatermark = 1000,
                                   Linger = TimeSpan.Zero
                               };
            var synodConfig = new kino.Consensus.Configuration.SynodConfiguration(new SynodConfigurationProvider(appConfig.Synod));
            var loggerMock = new Mock<ILogger>();
            var intercomMessageHub = new IntercomMessageHub(new SocketFactory(socketConfig),
                                                            synodConfig,
                                                            loggerMock.Object);
            var ballotGenerator = new BallotGenerator(appConfig.Lease);
            var roundBasedRegister = new RoundBasedRegister(intercomMessageHub,
                                                            ballotGenerator,
                                                            synodConfig,
                                                            appConfig.Lease,
                                                            loggerMock.Object);

            Thread.Sleep(appConfig.Lease.MaxLeaseTimeSpan);
            Thread.Sleep(TimeSpan.FromMilliseconds(300));

            return new RoundBasedRegisterTestSetup(ballotGenerator, synodConfig.LocalNode, roundBasedRegister);
        }

        internal static Uri[] GetSynodMembers()
        {            
            return new[]
                   {
                       new Uri("tcp://127.0.0.1:3001"),
                       new Uri("tcp://127.0.0.2:3002"),
                       new Uri("tcp://127.0.0.3:3003")
                   };
        }
    }
}