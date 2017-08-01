using System;
using System.Linq;
using System.Threading;
using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Consensus.Messages;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Consensus
{
    [TestFixture]
    public class IntercomMessageHubTests
    {
        private IntercomMessageHub messageHub;
        private Mock<ISocketFactory> socketFactory;
        private Mock<ISynodConfigurationProvider> synodConfigProvider;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> perfCounterManager;
        private Mock<IPerformanceCounter> perfCounter;
        private Mock<ILogger> logger;
        private Mock<ISocket> publisherSocket;
        private Mock<ISocket> subscriberSocket;

        [SetUp]
        public void Setup()
        {
            socketFactory = new Mock<ISocketFactory>();
            publisherSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreatePublisherSocket()).Returns(publisherSocket.Object);
            subscriberSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(subscriberSocket.Object);

            synodConfigProvider = new Mock<ISynodConfigurationProvider>();
            var synod = 3.Produce(i => new Location($"tcp://127.0.0.1:800{i}"));
            synodConfigProvider.Setup(m => m.Synod).Returns(synod);
            synodConfigProvider.Setup(m => m.HeartBeatInterval).Returns(TimeSpan.FromSeconds(2));
            synodConfigProvider.Setup(m => m.MissingHeartBeatsBeforeReconnect).Returns(2);
            synodConfigProvider.Setup(m => m.LocalNode).Returns(new Node(synod.First().Uri, ReceiverIdentifier.CreateIdentity()));
            synodConfigProvider.Setup(m => m.IntercomEndpoint).Returns(new Uri("inproc://health"));

            perfCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            perfCounter = new Mock<IPerformanceCounter>();
            perfCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            logger = new Mock<ILogger>();

            messageHub = new IntercomMessageHub(socketFactory.Object,
                                                synodConfigProvider.Object,
                                                perfCounterManager.Object,
                                                logger.Object);
        }

        [Test]
        public void IfSynodConsistsOfMoreThanOneNode_HeartBeatingIsStarted()
        {
            messageHub.Start(TimeSpan.FromSeconds(3));
            synodConfigProvider.Object.HeartBeatInterval.MultiplyBy(2).Sleep();
            messageHub.Stop();
            //
            Assert.Less(1, synodConfigProvider.Object.Synod.Count());
            publisherSocket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => msg.Equals(MessageIdentifier.Create<HeartBeatMessage>()))), Times.AtLeast(1));
        }

        [Test]
        public void IfSynodConsistsOfOneNode_NoHeartBeatingStarted()
        {
            var localNode = new Node("tcp://127.0.0.1:800", ReceiverIdentifier.CreateIdentity());
            synodConfigProvider.Setup(m => m.Synod).Returns(1.Produce(i => new Location(localNode.Uri.AbsoluteUri)));
            synodConfigProvider.Setup(m => m.LocalNode).Returns(localNode);

            messageHub = new IntercomMessageHub(socketFactory.Object,
                                                synodConfigProvider.Object,
                                                perfCounterManager.Object,
                                                logger.Object);
            //
            messageHub.Start(TimeSpan.FromSeconds(3));
            synodConfigProvider.Object.HeartBeatInterval.MultiplyBy(2).Sleep();
            messageHub.Stop();
            //
            Assert.AreEqual(1, synodConfigProvider.Object.Synod.Count());
            publisherSocket.Verify(m => m.SendMessage(It.IsAny<IMessage>()), Times.Never);
        }

        [Test]
        public void IfSomeClusterNodeIsNotHealthy_ConnectionMessageIsSent()
        {
            messageHub.Start(TimeSpan.FromSeconds(3));
            synodConfigProvider.Object.HeartBeatInterval.MultiplyBy(synodConfigProvider.Object.MissingHeartBeatsBeforeReconnect + 1).Sleep();
            messageHub.Stop();
            //
            var clusterHealthInfo = messageHub.GetClusterHealthInfo();
            Assert.IsTrue(clusterHealthInfo.All(hi => !hi.IsHealthy()));
            publisherSocket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => msg.Equals(MessageIdentifier.Create<ReconnectClusterMemberMessage>()))),
                                   Times.AtLeast(clusterHealthInfo.Count()));
        }

        [Test]
        public void IfReconnectMessageArrives_ConnectionToNodeIsReestablished()
        {
            var messageCount = 1;
            var deadNode = messageHub.GetClusterHealthInfo().First();
            var message = Message.Create(new ReconnectClusterMemberMessage
                                         {
                                             OldUri = deadNode.NodeUri.ToSocketAddress(),
                                             NewUri = deadNode.NodeUri.ToSocketAddress()
                                         });
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => messageCount-- > 0 ? message : null);
            synodConfigProvider.Object
                               .HeartBeatInterval
                               .MultiplyBy(synodConfigProvider.Object.MissingHeartBeatsBeforeReconnect + 1)
                               .Sleep();
            //
            Assert.IsFalse(deadNode.IsHealthy());
            var timeout = TimeSpan.FromSeconds(2);
            var now = DateTime.UtcNow;
            messageHub.Start(timeout);
            timeout.Sleep();
            messageHub.Stop();
            //
            subscriberSocket.Verify(m => m.Disconnect(deadNode.NodeUri), Times.Once);
            subscriberSocket.Verify(m => m.Connect(deadNode.NodeUri, false), Times.Once);
            Assert.LessOrEqual(now, deadNode.LastReconnectAttempt);
            Assert.IsFalse(deadNode.IsHealthy());
        }

        [Test]
        public void WhenHeartBeatMessageArrives_LastKnownHeartBeatIsUpdated()
        {
            synodConfigProvider.Setup(m => m.MissingHeartBeatsBeforeReconnect).Returns(1);
            messageHub = new IntercomMessageHub(socketFactory.Object,
                                                synodConfigProvider.Object,
                                                perfCounterManager.Object,
                                                logger.Object);
            var messageCount = 1;
            var deadNode = messageHub.GetClusterHealthInfo().First();
            var message = Message.Create(new HeartBeatMessage {NodeUri = deadNode.NodeUri.ToSocketAddress()});
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => messageCount-- > 0 ? message : null);

            synodConfigProvider.Object
                               .HeartBeatInterval
                               .MultiplyBy(synodConfigProvider.Object.MissingHeartBeatsBeforeReconnect + 1)
                               .Sleep();
            var timeout = synodConfigProvider.Object
                                             .HeartBeatInterval
                                             .DivideBy(2);
            //
            Assert.IsFalse(deadNode.IsHealthy());
            messageHub.Start(timeout);
            timeout.Sleep();
            messageHub.Stop();
            //
            Assert.IsTrue(deadNode.IsHealthy());
        }

        [Test]
        public void WhenMessageArrives_SubscriberIsNotified()
        {
            var messageCount = 1;
            var message = Message.Create(new SimpleMessage());
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => messageCount-- > 0 ? message : null);
            var observer = new Mock<IObserver<IMessage>>();
            messageHub.Subscribe().Subscribe(observer.Object);
            //
            var timeout = TimeSpan.FromSeconds(2);
            messageHub.Start(timeout);
            timeout.Sleep();
            messageHub.Stop();
            //
            observer.Verify(m => m.OnNext(message), Times.Once);
        }

        [Test]
        public void Send_SendsMessageToSocket()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var timeout = TimeSpan.FromSeconds(1);
            messageHub.Start(timeout);
            messageHub.Send(message);
            timeout.Sleep();
            messageHub.Stop();
            //
            publisherSocket.Verify(m => m.SendMessage(message), Times.Once);
        }
        
    }
}