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
        private Mock<ISynodConfiguration> synodConfig;
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

            synodConfig = new Mock<ISynodConfiguration>();
            var synod = 3.Produce(i => new Uri($"tcp://127.0.0.1:800{i}"));
            synodConfig.Setup(m => m.Synod).Returns(synod);
            synodConfig.Setup(m => m.HeartBeatInterval).Returns(TimeSpan.FromSeconds(2));
            synodConfig.Setup(m => m.MissingHeartBeatsBeforeReconnect).Returns(2);
            synodConfig.Setup(m => m.LocalNode).Returns(new Node(synod.First(), ReceiverIdentifier.CreateIdentity()));
            synodConfig.Setup(m => m.IntercomEndpoint).Returns(new Uri("inproc://health"));

            perfCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            perfCounter = new Mock<IPerformanceCounter>();
            perfCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            logger = new Mock<ILogger>();

            messageHub = new IntercomMessageHub(socketFactory.Object,
                                                synodConfig.Object,
                                                perfCounterManager.Object,
                                                logger.Object);
        }

        [Test]
        public void IfSynodConsistsOfMoreThanOneNode_HeartBeatingIsStarted()
        {
            messageHub.Start(TimeSpan.FromSeconds(3));
            synodConfig.Object.HeartBeatInterval.MultiplyBy(2).Sleep();
            //
            Assert.Less(1, synodConfig.Object.Synod.Count());
            publisherSocket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => msg.Equals(MessageIdentifier.Create<HeartBeatMessage>()))), Times.AtLeast(1));
        }

        [Test]
        public void IfSynodConsistsOfOneNode_NoHeartBeatingStarted()
        {
            var localNode = new Node("tcp://127.0.0.1:800", ReceiverIdentifier.CreateIdentity());
            synodConfig.Setup(m => m.Synod).Returns(1.Produce(i => localNode.Uri));
            synodConfig.Setup(m => m.LocalNode).Returns(localNode);

            messageHub = new IntercomMessageHub(socketFactory.Object,
                                                synodConfig.Object,
                                                perfCounterManager.Object,
                                                logger.Object);
            //
            messageHub.Start(TimeSpan.FromSeconds(3));
            synodConfig.Object.HeartBeatInterval.MultiplyBy(2).Sleep();
            //
            Assert.AreEqual(1, synodConfig.Object.Synod.Count());
            publisherSocket.Verify(m => m.SendMessage(It.IsAny<IMessage>()), Times.Never);
        }

        [Test]
        public void IfSomeClusterNodeIsNotHealthy_ConnectionMessageIsSent()
        {
            messageHub.Start(TimeSpan.FromSeconds(3));
            synodConfig.Object.HeartBeatInterval.MultiplyBy(synodConfig.Object.MissingHeartBeatsBeforeReconnect + 1).Sleep();
            //
            var clusterHealthInfo = messageHub.GetClusterHealthInfo();
            Assert.IsTrue(clusterHealthInfo.All(hi => !hi.IsHealthy()));
            publisherSocket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => msg.Equals(MessageIdentifier.Create<ReconnectClusterMemberMessage>()))),
                                   Times.AtLeast(2 * clusterHealthInfo.Count()));
        }

        [Test]
        public void IfReconnectMessageArrives_ConnectionToNodeIsReestablished()
        {
            var messageCount = 1;
            var deadNode = messageHub.GetClusterHealthInfo().First();
            var message = Message.Create(new ReconnectClusterMemberMessage {NodeUri = deadNode.NodeUri.ToSocketAddress()});
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => messageCount-- > 0 ? message : null);
            //
            var timeout = TimeSpan.FromSeconds(3);
            var now = DateTime.UtcNow;
            messageHub.Start(timeout);
            timeout.Sleep();
            messageHub.Stop();
            //
            subscriberSocket.Verify(m => m.Disconnect(deadNode.NodeUri), Times.Once);
            subscriberSocket.Verify(m => m.Connect(deadNode.NodeUri, false), Times.Once);
            Assert.LessOrEqual(now, deadNode.LastKnownHeartBeat);
        }

        [Test]
        public void WhenHeartBeatMessageArrives_LastKnownHeartBeatIsUpdated()
        {
            synodConfig.Setup(m => m.MissingHeartBeatsBeforeReconnect).Returns(1);
            messageHub = new IntercomMessageHub(socketFactory.Object,
                                                synodConfig.Object,
                                                perfCounterManager.Object,
                                                logger.Object);
            var messageCount = 1;
            var deadNode = messageHub.GetClusterHealthInfo().First();
            var message = Message.Create(new HeartBeatMessage {NodeUri = deadNode.NodeUri.ToSocketAddress()});
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => messageCount-- > 0 ? message : null);

            synodConfig.Object
                       .HeartBeatInterval
                       .MultiplyBy(synodConfig.Object.MissingHeartBeatsBeforeReconnect + 1)
                       .Sleep();
            var timeout = synodConfig.Object
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
        public void Broadcast_SendsMessageToAllSubscribers()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var timeout = TimeSpan.FromSeconds(1);
            messageHub.Start(timeout);
            messageHub.Broadcast(message);
            timeout.Sleep();
            messageHub.Stop();
            //
            Assert.IsTrue(Unsafe.ArraysEqual(new byte[0], message.SocketIdentity));
        }

        [Test]
        public void Send_SendsMessageToOneSubscribers()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var receiver = ReceiverIdentifier.CreateIdentity();
            var timeout = TimeSpan.FromSeconds(1);
            messageHub.Start(timeout);
            messageHub.Send(message, receiver);
            timeout.Sleep();
            messageHub.Stop();
            //
            Assert.IsTrue(Unsafe.ArraysEqual(receiver, message.SocketIdentity));
        }
    }
}