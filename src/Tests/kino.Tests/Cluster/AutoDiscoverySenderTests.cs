using System;
using System.Linq;
using System.Threading;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    public class AutoDiscoverySenderTests
    {
        private TimeSpan AsyncOp = TimeSpan.FromSeconds(1);
        private AutoDiscoverySender autoDiscoverSender;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private Mock<ILogger> logger;
        private Mock<ISocketFactory> socketFactory;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private Mock<ISocket> socket;
        private RendezvousEndpoint rendezvousEndpoint;
        private ClusterMembershipConfiguration config;

        [SetUp]
        public void Setup()
        {
            rendezvousCluster = new Mock<IRendezvousCluster>();
            rendezvousEndpoint = new RendezvousEndpoint("tcp://*:8080", "tcp://*:9009");
            rendezvousCluster.Setup(m => m.GetCurrentRendezvousServer()).Returns(rendezvousEndpoint);
            socketFactory = new Mock<ISocketFactory>();
            socket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket.Object);
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            var perfCounter = new Mock<IPerformanceCounter>();
            performanceCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            logger = new Mock<ILogger>();
            config = new ClusterMembershipConfiguration
                     {
                         RouteDiscovery = new RouteDiscoveryConfiguration
                                          {
                                              MaxAutoDiscoverySenderQueueLength = 100
                                          }
                     };
            autoDiscoverSender = new AutoDiscoverySender(rendezvousCluster.Object,
                                                         socketFactory.Object,
                                                         config,
                                                         performanceCounterManager.Object,
                                                         logger.Object);
        }

        [Test]
        public void StartBlockingSendMessages_SendsEnqueuedMessages()
        {
            var messages = Randomizer.Int32(2, 5)
                                     .Produce(() => Message.Create(new SimpleMessage()))
                                     .ForEach(msg => autoDiscoverSender.EnqueueMessage(msg))
                                     .ToList();
            var tokenSource = new CancellationTokenSource(AsyncOp);
            var barrier = new Barrier(1);
            //
            autoDiscoverSender.StartBlockingSendMessages(tokenSource.Token, barrier);
            //
            socket.Verify(m => m.SendMessage(It.IsAny<IMessage>()), Times.Exactly(messages.Count()));
        }

        [Test]
        public void MessageIsNotEnqueued_IfQueueLengthIsGreaterThanMaxAutoDiscoverySenderQueueLength()
        {
            config.RouteDiscovery.MaxAutoDiscoverySenderQueueLength = Randomizer.Int32(10, 20);
            (config.RouteDiscovery.MaxAutoDiscoverySenderQueueLength + 1)
               .Produce(() => Message.Create(new SimpleMessage()))
               .ForEach(msg => autoDiscoverSender.EnqueueMessage(msg))
               .ToList();
            var tokenSource = new CancellationTokenSource(AsyncOp);
            var barrier = new Barrier(1);
            //
            autoDiscoverSender.StartBlockingSendMessages(tokenSource.Token, barrier);
            //
            socket.Verify(m => m.SendMessage(It.IsAny<IMessage>()), Times.Exactly(config.RouteDiscovery.MaxAutoDiscoverySenderQueueLength));
        }
    }
}