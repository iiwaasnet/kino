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
using Xunit;
using Health = kino.Cluster.Health;

namespace kino.Tests.Cluster
{
    public class ClusterHealthMonitorTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan ReceiveMessageDelay = TimeSpan.FromMilliseconds(2000);
        private static readonly TimeSpan ReceiveMessageCompletionDelay = ReceiveMessageDelay + TimeSpan.FromMilliseconds(1000);
        private readonly Mock<ISocketFactory> socketFactory;
        private readonly Mock<ISocket> publisherSocket;
        private readonly ClusterHealthMonitor clusterHealthMonitor;
        private readonly Mock<ISocket> subscriberSocket;
        private readonly Mock<ISocket> routerSocket;
        private readonly Mock<ILocalSocketFactory> localSocketFactory;
        private readonly Mock<ILocalSocket<IMessage>> multiplexingSocket;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly Mock<ILocalSendingSocket<IMessage>> routerLocalSocket;
        private readonly ClusterHealthMonitorConfiguration config;
        private readonly Mock<ILogger> logger;
        private readonly Mock<IConnectedPeerRegistry> connectedPeerRegistry;
        private readonly CancellationTokenSource tokenSource;

        public ClusterHealthMonitorTests()
        {
            tokenSource = new CancellationTokenSource();
            socketFactory = new Mock<ISocketFactory>();
            publisherSocket = new Mock<ISocket>();
            subscriberSocket = new Mock<ISocket>();
            routerSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(routerSocket.Object);
            socketFactory.Setup(m => m.CreatePublisherSocket()).Returns(publisherSocket.Object);
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(subscriberSocket.Object);
            localSocketFactory = new Mock<ILocalSocketFactory>();
            multiplexingSocket = new Mock<ILocalSocket<IMessage>>();
            multiplexingSocket.Setup(m => m.CanReceive()).Returns(new ManualResetEvent(false));
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

        [Fact]
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
                                                                    Assert.True(Unsafe.ArraysEqual(peer.SocketIdentity, payload.SocketIdentity));
                                                                    Assert.Equal(peer.Uri.ToSocketAddress(), payload.Uri);
                                                                    Assert.Equal(health.Uri, payload.Health.Uri);
                                                                    Assert.Equal(health.HeartBeatInterval, payload.Health.HeartBeatInterval);
                                                                    return true;
                                                                }

                                                                return false;
                                                            };
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isStartMonitoringMessage(msg))), Times.Once);
        }

        [Fact]
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
                                                            Assert.True(Unsafe.ArraysEqual(peer.SocketIdentity, payload.SocketIdentity));
                                                            Assert.Equal(peer.Uri.ToSocketAddress(), payload.Uri);
                                                            Assert.Equal(health.Uri, payload.Health.Uri);
                                                            Assert.Equal(health.HeartBeatInterval, payload.Health.HeartBeatInterval);
                                                            return true;
                                                        }

                                                        return false;
                                                    };
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isAddPeerMessage(msg))), Times.Once);
        }

        [Fact]
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
                                                               Assert.True(Unsafe.ArraysEqual(receiverIdentifier.Identity, payload.NodeIdentity));
                                                               return true;
                                                           }

                                                           return false;
                                                       };
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isDeletePeerMessage(msg))), Times.Once);
        }

        [Fact]
        public void MessageReceiverOverMultiplexingSocket_IsSentToPublisherSocket()
        {
            var message = Message.Create(new SimpleMessage());
            multiplexingSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            clusterHealthMonitor.Start();
            ReceiveMessageCompletionDelay.Sleep();
            clusterHealthMonitor.Stop();
            //
            logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
            publisherSocket.Verify(m => m.SendMessage(message), Times.Once);
        }

        [Fact]
        public void WhenClusterHealthMonitorStarts_ItStartsSendingCheckStalePeersMessage()
        {
            config.StalePeersCheckInterval = TimeSpan.FromMilliseconds(200);
            //
            clusterHealthMonitor.Start();
            config.StalePeersCheckInterval.MultiplyBy(20).Sleep();
            clusterHealthMonitor.Stop();
            //
            logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => msg.Equals(KinoMessages.CheckStalePeers))), Times.AtLeastOnce);
        }

        [Fact]
        public void IfStartPeerMonitoringMessageReceived_ConnectionToPeerHealthUriEsteblished()
        {
            var healthUri = new Uri("tcp://127.0.0.2:9090");
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new StartPeerMonitoringMessage
                          {
                              Uri = "tcp://127.0.0.1:800",
                              SocketIdentity = peerIdentifier.Identity,
                              Health = new kino.Messaging.Messages.Health
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
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
            subscriberSocket.Verify(m => m.Connect(healthUri, false), Times.Once);
            Assert.True(meta.ConnectionEstablished);
        }

        [Fact]
        public void IfStartPeerMonitoringMessadeReceived_CheckDeadPeersMessageAfterPeerHeartBeatInterval()
        {
            config.StalePeersCheckInterval = TimeSpan.FromMinutes(1);
            var healthUri = new Uri("tcp://127.0.0.2:9090");
            var heartBeatInterval = TimeSpan.FromMilliseconds(500);
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new StartPeerMonitoringMessage
                          {
                              Uri = "tcp://127.0.0.1:800",
                              SocketIdentity = peerIdentifier.Identity,
                              Health = new kino.Messaging.Messages.Health
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
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            //
            clusterHealthMonitor.Start();
            heartBeatInterval.MultiplyBy(2).Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            multiplexingSocket.Verify(m => m.Send(It.Is<IMessage>(msg => msg.Equals(KinoMessages.CheckDeadPeers))), Times.AtLeastOnce);
        }

        [Fact]
        public void WhenHeartBeatMessageArrives_PeerLastKnwonHeartBeatIsSetToUtcNow()
        {
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new HeartBeatMessage {SocketIdentity = peerIdentifier.Identity};
            var message = Message.Create(payload);
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            var meta = new ClusterMemberMeta {LastKnownHeartBeat = DateTime.UtcNow - TimeSpan.FromHours(20)};
            connectedPeerRegistry.Setup(m => m.Find(peerIdentifier)).Returns(meta);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            Assert.InRange(DateTime.UtcNow - meta.LastKnownHeartBeat, TimeSpan.MinValue, AsyncOp.MultiplyBy(3));
        }

        [Fact]
        public void WhenHeartBeatMessageArrivesFromUnknownPeer_ItsHealthUriIsDisconnected()
        {
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var healthUri = new Uri("tcp://127.0.0.1:80");
            var payload = new HeartBeatMessage
                          {
                              SocketIdentity = peerIdentifier.Identity,
                              HealthUri = healthUri.ToSocketAddress()
                          };
            var message = Message.Create(payload);
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            connectedPeerRegistry.Setup(m => m.Find(peerIdentifier)).Returns((ClusterMemberMeta) null);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            subscriberSocket.Verify(m => m.Disconnect(healthUri), Times.Once);
        }

        [Fact]
        public void WhenAddPeerMessageArrives_PeerIsAddedToConnectedPeerRegistry()
        {
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new AddPeerMessage
                          {
                              SocketIdentity = peerIdentifier.Identity,
                              Uri = "tcp://127.0.0.1:8080",
                              Health = new kino.Messaging.Messages.Health
                                       {
                                           Uri = "tcp://127.0.0.2:9090",
                                           HeartBeatInterval = TimeSpan.FromSeconds(10)
                                       }
                          };
            var message = Message.Create(payload);
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            Func<ClusterMemberMeta, bool> isPeerMetadata = meta =>
                                                               payload.Health.Uri == meta.HealthUri
                                                               && payload.Health.HeartBeatInterval == meta.HeartBeatInterval
                                                               && payload.Uri == meta.ScaleOutUri;
            connectedPeerRegistry.Verify(m => m.FindOrAdd(peerIdentifier, It.Is<ClusterMemberMeta>(meta => isPeerMetadata(meta))), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhenDeletePeerMessageArrives_PeerIsRemovedFromRegistryAndHealthUriDisconnected(bool connectionEstablished)
        {
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new DeletePeerMessage {NodeIdentity = peerIdentifier.Identity};
            var message = Message.Create(payload);
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            var meta = new ClusterMemberMeta
                       {
                           HealthUri = "tcp://127.0.0.2:9009",
                           ConnectionEstablished = connectionEstablished
                       };
            connectedPeerRegistry.Setup(m => m.Find(peerIdentifier)).Returns(meta);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            connectedPeerRegistry.Verify(m => m.Remove(peerIdentifier), Times.Once);
            subscriberSocket.Verify(m => m.Disconnect(new Uri(meta.HealthUri)), Times.Exactly(connectionEstablished ? 1 : 0));
        }

        [Fact]
        public void WhenCheckPeerConnectionMessageArrives_ConnectionToPeerEstablishedAndMessageIsSent()
        {
            var peerIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var payload = new CheckPeerConnectionMessage {SocketIdentity = peerIdentifier.Identity};
            var message = Message.Create(payload);
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            var meta = new ClusterMemberMeta
                       {
                           ScaleOutUri = "tcp://127.0.0.2:9009",
                           ConnectionEstablished = false,
                           LastKnownHeartBeat = DateTime.UtcNow - TimeSpan.FromMinutes(30)
                       };
            connectedPeerRegistry.Setup(m => m.Find(peerIdentifier)).Returns(meta);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            socketFactory.Verify(m => m.CreateRouterSocket(), Times.Once);
            routerSocket.Verify(m => m.SetMandatoryRouting(true), Times.Once);
            routerSocket.Verify(m => m.Connect(new Uri(meta.ScaleOutUri), true), Times.Once);
            routerSocket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => msg.Equals(KinoMessages.Ping))), Times.Once);
            routerSocket.Verify(m => m.Disconnect(new Uri(meta.ScaleOutUri)), Times.Once);
            Assert.InRange(DateTime.UtcNow - meta.LastKnownHeartBeat, TimeSpan.MinValue, AsyncOp.MultiplyBy(3));
        }

        [Fact]
        public void WhenCheckDeadPeersMessageArrives_ForEveryPeerWithExpiredHeartBeatUnregisterUnreachableNodeMessageIsSent()
        {
            var message = Message.Create(new CheckDeadPeersMessage());
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            var deadPeers = Randomizer.Int32(3, 5)
                                      .Produce(() => new KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>(new ReceiverIdentifier(Guid.NewGuid().ToByteArray()), new ClusterMemberMeta()))
                                      .ToList();
            connectedPeerRegistry.Setup(m => m.GetPeersWithExpiredHeartBeat()).Returns(deadPeers);
            //
            clusterHealthMonitor.Start();
            AsyncOp.Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            Func<IMessage, bool> isUnregisterNodeMessage = msg =>
                                                           {
                                                               if (msg.Equals(KinoMessages.UnregisterUnreachableNode))
                                                               {
                                                                   var payload = msg.GetPayload<UnregisterUnreachableNodeMessage>();
                                                                   Assert.True(deadPeers.Any(kv => kv.Key == new ReceiverIdentifier(payload.ReceiverNodeIdentity)));
                                                                   return true;
                                                               }

                                                               return false;
                                                           };
            routerLocalSocket.Verify(m => m.Send(It.Is<IMessage>(msg => isUnregisterNodeMessage(msg))), Times.Exactly(deadPeers.Count));
        }

        [Fact]
        public void WhenCheckStalePeersMessageArrives_ConnectionToPeerEstablishedAndMessageIsSentToEachStalePeer()
        {
            var payload = new CheckStalePeersMessage();
            var message = Message.Create(payload);
            subscriberSocket.SetupMessageReceived(message, tokenSource.Token);
            var stalePeers = Randomizer.Int32(3, 5)
                                       .Produce(i => new KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>(new ReceiverIdentifier(Guid.NewGuid().ToByteArray()),
                                                                                                             new ClusterMemberMeta
                                                                                                             {
                                                                                                                 ScaleOutUri = $"tcp://127.0.0.1:{i + 1000}"
                                                                                                             }))
                                       .ToList();
            connectedPeerRegistry.Setup(m => m.GetStalePeers()).Returns(stalePeers);
            //
            clusterHealthMonitor.Start();
            TimeSpan.FromSeconds(2).Sleep();
            tokenSource.Cancel();
            clusterHealthMonitor.Stop();
            //
            Func<Uri, bool> isStalePeerUri = uri =>
                                             {
                                                 Assert.True(stalePeers.Any(kv => kv.Value.ScaleOutUri == uri.ToSocketAddress()));
                                                 return true;
                                             };

            var callTimes = Times.Exactly(stalePeers.Count);
            socketFactory.Verify(m => m.CreateRouterSocket(), callTimes);
            routerSocket.Verify(m => m.SetMandatoryRouting(true), callTimes);
            routerSocket.Verify(m => m.Connect(It.Is<Uri>(uri => isStalePeerUri(uri)), true), callTimes);
            routerSocket.Verify(m => m.SendMessage(It.Is<IMessage>(msg => msg.Equals(KinoMessages.Ping))), callTimes);
            routerSocket.Verify(m => m.Disconnect(It.Is<Uri>(uri => isStalePeerUri(uri))), callTimes);
        }
    }
}