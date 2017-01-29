using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;
using Health = kino.Cluster.Health;

namespace kino.Tests.Cluster
{
    [TestFixture]
    public class ClusterHealthMonitorTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan ReceiveMessageDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan ReceiveMessageCompletionDelay = ReceiveMessageDelay + TimeSpan.FromMilliseconds(1000);
        private Mock<ISocketFactory> socketFactory;
        private Mock<ISocket> publisherSocket;
        private ClusterHealthMonitor clusterHealthMonitor;
        private Mock<ISocket> subscriberSocket;
        private Mock<ISocket> routerSocket;
        private Mock<ILocalSocketFactory> localSocketFactory;
        private Mock<ILocalSocket<IMessage>> multiplexingSocket;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ILocalSendingSocket<IMessage>> routerLocalSocket;
        private ClusterHealthMonitorConfiguration config;
        private Mock<ILogger> logger;
        private Mock<IConnectedPeerRegistry> connectedPeerRegistry;

        [SetUp]
        public void Setup()
        {
            socketFactory = new Mock<ISocketFactory>();
            publisherSocket = new Mock<ISocket>();
            subscriberSocket = new Mock<ISocket>();
            routerSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(routerSocket.Object);
            socketFactory.Setup(m => m.CreatePublisherSocket()).Returns(publisherSocket.Object);
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(subscriberSocket.Object);
            localSocketFactory = new Mock<ILocalSocketFactory>();
            multiplexingSocket = new Mock<ILocalSocket<IMessage>>();
            localSocketFactory.Setup(m => m.Create<IMessage>()).Returns(multiplexingSocket.Object);
            securityProvider = new Mock<ISecurityProvider>();
            var pingDomain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(pingDomain);
            routerLocalSocket = new Mock<ILocalSendingSocket<IMessage>>();
            config = new ClusterHealthMonitorConfiguration
                     {
                         IntercomEndpoint = new Uri("tcp://127.0.0.1:8087"),
                         StalePeersCheckInterval = TimeSpan.FromMinutes(1)
                     };
            logger = new Mock<ILogger>();
            connectedPeerRegistry = new Mock<IConnectedPeerRegistry>();
            connectedPeerRegistry.Setup(m => m.GetPeersWithExpiredHeartBeat()).Returns(Enumerable.Empty<KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>>());
            clusterHealthMonitor = new ClusterHealthMonitor(socketFactory.Object,
                                                            localSocketFactory.Object,
                                                            securityProvider.Object,
                                                            routerLocalSocket.Object,
                                                            connectedPeerRegistry.Object,
                                                            config,
                                                            logger.Object);
        }

        [Test]
        public void StartPeerMonitoring_SendsStartPeerMonitoringMessage()
        {
            var peer = new Node("tcp://127.0.0.1:8080", Guid.NewGuid().ToByteArray());
            var health = new Health
                         {
                             Uri = "tcp://127.0.0.2:9090",
                             HeartBeatInterval = TimeSpan.FromSeconds(3)
                         };
            //
            clusterHealthMonitor.StartPeerMonitoring(peer, health);
            //
            Func<IMessage, bool> isStartMonitoringMessage = msg =>
                                                            {
                                                                if (msg.Equals(KinoMessages.StartPeerMonitoring))
                                                                {
                                                                    var payload = msg.GetPayload<StartPeerMonitoringMessage>();
                                                                    Assert.IsTrue(Unsafe.ArraysEqual(peer.SocketIdentity, payload.SocketIdentity));
                                                                    Assert.AreEqual(peer.Uri.ToSocketAddress(), payload.Uri);
                                                                    Assert.AreEqual(health.Uri, payload.Health.Uri);
                                                                    Assert.AreEqual(health.HeartBeatInterval, payload.Health.HeartBeatInterval);
                                                                    return true;
                                                                }

                                                                return false;
                                                            };
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isStartMonitoringMessage(msg))), Times.Once);
        }

        [Test]
        public void AddPeer_SendsAddPeerMessage()
        {
            var peer = new Node("tcp://127.0.0.1:8080", Guid.NewGuid().ToByteArray());
            var health = new Health
                         {
                             Uri = "tcp://127.0.0.2:9090",
                             HeartBeatInterval = TimeSpan.FromSeconds(3)
                         };
            //
            clusterHealthMonitor.AddPeer(peer, health);
            //
            Func<IMessage, bool> isAddPeerMessage = msg =>
                                                    {
                                                        if (msg.Equals(KinoMessages.AddPeer))
                                                        {
                                                            var payload = msg.GetPayload<AddPeerMessage>();
                                                            Assert.IsTrue(Unsafe.ArraysEqual(peer.SocketIdentity, payload.SocketIdentity));
                                                            Assert.AreEqual(peer.Uri.ToSocketAddress(), payload.Uri);
                                                            Assert.AreEqual(health.Uri, payload.Health.Uri);
                                                            Assert.AreEqual(health.HeartBeatInterval, payload.Health.HeartBeatInterval);
                                                            return true;
                                                        }

                                                        return false;
                                                    };
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isAddPeerMessage(msg))), Times.Once);
        }

        [Test]
        public void DeletePeer_SendsDeletePeerMessage()
        {
            var receiverIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            //
            clusterHealthMonitor.DeletePeer(receiverIdentifier);
            //
            Func<IMessage, bool> isDeletePeerMessage = msg =>
                                                       {
                                                           if (msg.Equals(KinoMessages.DeletePeer))
                                                           {
                                                               var payload = msg.GetPayload<DeletePeerMessage>();
                                                               Assert.IsTrue(Unsafe.ArraysEqual(receiverIdentifier.Identity, payload.NodeIdentity));
                                                               return true;
                                                           }

                                                           return false;
                                                       };
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isDeletePeerMessage(msg))), Times.Once);
        }

        [Test]
        public void MessageReceiverOverMultiplexingSocket_IsSentToPublisherSocket()
        {
            var message = Message.Create(new SimpleMessage());
            multiplexingSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            clusterHealthMonitor.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            publisherSocket.Verify(m => m.SendMessage(message), Times.Once);
        }

        [Test]
        public void WhenClusterHealthMonitorStarts_ItStartsSendingCheckStalePeersMessage()
        {
            config.StalePeersCheckInterval = TimeSpan.FromMilliseconds(500);
            //
            clusterHealthMonitor.Start();
            config.StalePeersCheckInterval.MultiplyBy(2).Sleep();
            //
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => msg.Equals(KinoMessages.CheckStalePeers))), Times.AtLeastOnce);
        }

        [Test]
        public void IfStartPeerMonitoringMessadeReceived_ConnectsToPeerHealthUri()
        {
            var healthUri = new Uri("tcp://127.0.0.2:9090");
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new StartPeerMonitoringMessage
                          {
                              Uri = "tcp://127.0.0.1:800",
                              SocketIdentity = peerIdentifier.Identity,
                              Health = new global::kino.Messaging.Messages.Health
                                       {
                                           Uri = healthUri.ToSocketAddress(),
                                           HeartBeatInterval = TimeSpan.FromMinutes(1)
                                       }
                          };
            var message = Message.Create(payload);
            var meta = new ClusterMemberMeta
                       {
                           HealthUri = payload.Health.Uri,
                           HeartBeatInterval = payload.Health.HeartBeatInterval,
                           ScaleOutUri = payload.Uri,
                           LastKnownHeartBeat = DateTime.UtcNow,
                           ConnectionEstablished = false
                       };
            connectedPeerRegistry.Setup(m => m.FindOrAdd(It.Is<ReceiverIdentifier>(id => id == peerIdentifier), It.IsAny<ClusterMemberMeta>())).Returns(meta);
            var times = 0;
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => times++ == 0 ? message : null);
            //
            clusterHealthMonitor.Start();
            TimeSpan.FromMilliseconds(100).Sleep();
            clusterHealthMonitor.Stop();
            //
            subscriberSocket.Verify(m => m.Connect(healthUri, false), Times.Once);
            Assert.IsTrue(meta.ConnectionEstablished);
        }

        [Test]
        public void IfStartPeerMonitoringMessadeReceived_CheckDeadPeersMessageAfterPeerHeartBeatInterval()
        {
            config.StalePeersCheckInterval = TimeSpan.FromMinutes(1);
            var healthUri = new Uri("tcp://127.0.0.2:9090");
            var heartBeatInterval = TimeSpan.FromMilliseconds(100);
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new StartPeerMonitoringMessage
                          {
                              Uri = "tcp://127.0.0.1:800",
                              SocketIdentity = peerIdentifier.Identity,
                              Health = new global::kino.Messaging.Messages.Health
                                       {
                                           Uri = healthUri.ToSocketAddress(),
                                           HeartBeatInterval = heartBeatInterval
                                       }
                          };
            var message = Message.Create(payload);
            var meta = new ClusterMemberMeta
                       {
                           HealthUri = payload.Health.Uri,
                           HeartBeatInterval = payload.Health.HeartBeatInterval,
                           ScaleOutUri = payload.Uri,
                           LastKnownHeartBeat = DateTime.UtcNow,
                           ConnectionEstablished = false
                       };
            connectedPeerRegistry.Setup(m => m.FindOrAdd(It.Is<ReceiverIdentifier>(id => id == peerIdentifier), It.IsAny<ClusterMemberMeta>())).Returns(meta);
            var times = 0;
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => times++ == 0 ? message : null);
            //
            clusterHealthMonitor.Start();
            heartBeatInterval.MultiplyBy(2).Sleep();
            clusterHealthMonitor.Stop();
            //
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => msg.Equals(KinoMessages.CheckDeadPeers))), Times.AtLeastOnce);
        }

        [Test]
        public void WhenHeartBeatMessageArrives_PeerLastKnwonHeartBeatIsSetToUtcNow()
        {
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new HeartBeatMessage {SocketIdentity = peerIdentifier.Identity};
            var message = Message.Create(payload);
            var times = 0;
            subscriberSocket.Setup(m => m.ReceiveMessage(It.IsAny<CancellationToken>())).Returns(() => times++ == 0 ? message : null);
            var meta = new ClusterMemberMeta {LastKnownHeartBeat = DateTime.UtcNow - TimeSpan.FromMinutes(30)};
            connectedPeerRegistry.Setup(m => m.Find(peerIdentifier)).Returns(meta);
            //
            clusterHealthMonitor.Start();
            //
            Assert.LessOrEqual(TimeSpan.FromMilliseconds(200), DateTime.UtcNow - meta.LastKnownHeartBeat);
        }
    }
}