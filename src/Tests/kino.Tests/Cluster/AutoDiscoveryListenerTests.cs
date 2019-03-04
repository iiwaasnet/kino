using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    public class AutoDiscoveryListenerTests
    {
        private readonly TimeSpan AsyncOp = TimeSpan.FromSeconds(1);
        private AutoDiscoveryListener autoDiscoveryListener;
        private ClusterMembershipConfiguration membershipConfiguration;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private Mock<ILogger> logger;
        private Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private Mock<ISocketFactory> socketFactory;
        private Mock<ILocalSocket<IMessage>> localRouterSocket;
        private Mock<ISocket> subscriptionSocket;
        private Mock<Action> restartRequestHandler;
        private Barrier gateway;
        private RendezvousEndpoint[] rendezvousEndpoints;
        private int currentRendezvousIndex;
        private SocketEndpoint scaleOutAddress;
        private Mock<ILocalSocketFactory> localSocketFactory;

        [SetUp]
        public void Setup()
        {
            rendezvousCluster = new Mock<IRendezvousCluster>();
            rendezvousEndpoints = new[]
                                  {
                                      new RendezvousEndpoint("tcp://*:8080", "tcp://*:9090"),
                                      new RendezvousEndpoint("tcp://*:8081", "tcp://*:9091")
                                  };
            currentRendezvousIndex = 0;
            rendezvousCluster.Setup(m => m.GetCurrentRendezvousServer()).Returns(GetCurrentRendezvous());
            rendezvousCluster.Setup(m => m.RotateRendezvousServers()).Callback(SetNextRendezvous);
            rendezvousCluster.Setup(m => m.Reconfigure(It.IsAny<IEnumerable<RendezvousEndpoint>>()))
                             .Callback<IEnumerable<RendezvousEndpoint>>(SetNewRendezvous);
            socketFactory = new Mock<ISocketFactory>();
            subscriptionSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(subscriptionSocket.Object);
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutAddress = SocketEndpoint.Parse("tcp://*:7878", Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            membershipConfiguration = new ClusterMembershipConfiguration
                                      {
                                          HeartBeatSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(1)
                                      };
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            var perfCounter = new Mock<IPerformanceCounter>();
            performanceCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            localRouterSocket = new Mock<ILocalSocket<IMessage>>();
            logger = new Mock<ILogger>();
            restartRequestHandler = new Mock<Action>();
            gateway = new Barrier(1);
            localSocketFactory = new Mock<ILocalSocketFactory>();
            localSocketFactory.Setup(m => m.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket))
                              .Returns(localRouterSocket.Object);

            autoDiscoveryListener = new AutoDiscoveryListener(rendezvousCluster.Object,
                                                              socketFactory.Object,
                                                              localSocketFactory.Object,
                                                              scaleOutConfigurationProvider.Object,
                                                              membershipConfiguration,
                                                              performanceCounterManager.Object,
                                                              logger.Object);
        }

        [Test]
        public async Task IfHeartBeatDoesNotArriveWithinHeartBeatSilenceBeforeRendezvousFailoverTime_ListenerRestartsConnectingToNextRendezvous()
        {
            var numberOfTimeouts = 3;
            var cancellationTokenSource = new CancellationTokenSource(membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover.MultiplyBy(numberOfTimeouts));
            var previousRendezvous = GetCurrentRendezvous();
            //
            await Start(() => autoDiscoveryListener.StartBlockingListenMessages(restartRequestHandler.Object, cancellationTokenSource.Token, gateway));
            //
            var restartTimes = numberOfTimeouts - 1;
            restartRequestHandler.Verify(m => m(), Times.AtLeast(restartTimes));
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.AtLeast(restartTimes));
            subscriptionSocket.Verify(m => m.Connect(previousRendezvous.BroadcastUri, true), Times.Once);
            subscriptionSocket.Verify(m => m.Connect(GetCurrentRendezvous().BroadcastUri, true), Times.Once);
        }

        [Test]
        public async Task IfHeartBeatArrivesWithinHeartBeatSilenceBeforeRendezvousFailoverTime_ListenerDoesntRestart()
        {
            var numberOfHeartBeats = 2;
            var cancellationTokenSource = new CancellationTokenSource(membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover.MultiplyBy(numberOfHeartBeats));
            var heartBeatFrequency = membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover.DivideBy(2);
            subscriptionSocket.SetupPeriodicMessageReceived(Message.Create(new HeartBeatMessage()), heartBeatFrequency);
            //
            await Start(() => autoDiscoveryListener.StartBlockingListenMessages(restartRequestHandler.Object, cancellationTokenSource.Token, gateway));
            //
            restartRequestHandler.Verify(m => m(), Times.Never);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.Never);
        }

        [Test]
        public async Task IfRendezvousConfigurationChangedMessageArrives_RendezvousIsReconfiguredAndListenerRestartsConnectingToNewRendezvous()
        {
            membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(10);
            var cancellationTokenSource = new CancellationTokenSource(AsyncOp);
            var payload = new RendezvousConfigurationChangedMessage
                          {
                              RendezvousNodes = Randomizer.Int32(5, 15)
                                                          .Produce(i => new RendezvousNode
                                                                        {
                                                                            BroadcastUri = $"tcp://*:{2000 + i}".ParseAddress().ToSocketAddress(),
                                                                            UnicastUri = $"tcp://*:{1000 + i}".ParseAddress().ToSocketAddress()
                                                                        })
                                                          .ToList()
                          };
            var message = Message.Create(payload);
            subscriptionSocket.SetupMessageReceived(message, cancellationTokenSource.Token);
            //
            await Start(() => autoDiscoveryListener.StartBlockingListenMessages(restartRequestHandler.Object, cancellationTokenSource.Token, gateway));
            //
            restartRequestHandler.Verify(m => m(), Times.Once);
            Func<IEnumerable<RendezvousEndpoint>, bool> areNewNodes = nodes =>
                                                                      {
                                                                          CollectionAssert.AreEquivalent(nodes,
                                                                                                         payload.RendezvousNodes
                                                                                                                .Select(rn => new RendezvousEndpoint(rn.UnicastUri, rn.BroadcastUri)));
                                                                          return true;
                                                                      };
            rendezvousCluster.Verify(m => m.Reconfigure(It.Is<IEnumerable<RendezvousEndpoint>>(nodes => areNewNodes(nodes))), Times.Once);
            Assert.AreEqual(GetCurrentRendezvous().BroadcastUri, payload.RendezvousNodes.First().BroadcastUri);
            restartRequestHandler.Verify(m => m(), Times.Once);
        }

        [Test]
        public async Task IfRendezvousNotLeaderMessageArrives_ListenerRestartsConnectingToNewRendezvousLeader()
        {
            membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(10);
            var cancellationTokenSource = new CancellationTokenSource(AsyncOp);
            var rendezvousEndpoint = rendezvousEndpoints[currentRendezvousIndex + 1];
            var payload = new RendezvousNotLeaderMessage
                          {
                              NewLeader = new RendezvousNode
                                          {
                                              BroadcastUri = rendezvousEndpoint.BroadcastUri,
                                              UnicastUri = rendezvousEndpoint.UnicastUri
                                          }
                          };
            var message = Message.Create(payload);
            subscriptionSocket.SetupMessageReceived(message, cancellationTokenSource.Token);
            //
            await Start(() => autoDiscoveryListener.StartBlockingListenMessages(restartRequestHandler.Object, cancellationTokenSource.Token, gateway));
            //
            restartRequestHandler.Verify(m => m(), Times.AtLeastOnce);
            Func<RendezvousEndpoint, bool> isNewLeader = rnd =>
                                                         {
                                                             Assert.AreEqual(payload.NewLeader.BroadcastUri, rnd.BroadcastUri);
                                                             Assert.AreEqual(payload.NewLeader.UnicastUri, rnd.UnicastUri);
                                                             return true;
                                                         };
            rendezvousCluster.Verify(m => m.SetCurrentRendezvousServer(It.Is<RendezvousEndpoint>(rnd => isNewLeader(rnd))), Times.AtLeastOnce);
            restartRequestHandler.Verify(m => m(), Times.AtLeastOnce);
        }

        [Test]
        public void RoutingControlMessages_AreForwardedToRouterSocket()
        {
            foreach (var payload in GetRoutingControlMessages())
            {
                TestRoutingControlMessagesAreForwardedToRouterSocket(payload);
            }
        }

        private void TestRoutingControlMessagesAreForwardedToRouterSocket(IPayload payload)
        {
            membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(100);
            var cancellationTokenSource = new CancellationTokenSource(AsyncOp);
            var message = Message.Create(payload);
            subscriptionSocket.SetupMessageReceived(message, cancellationTokenSource.Token);
            //
            var task = Start(() => autoDiscoveryListener.StartBlockingListenMessages(restartRequestHandler.Object, cancellationTokenSource.Token, gateway));
            task.Wait();
            //
            localRouterSocket.Verify(m => m.Send(message), Times.Once);
        }

        private IEnumerable<IPayload> GetRoutingControlMessages()
        {
            yield return new RequestClusterMessageRoutesMessage {RequestorNodeIdentity = Guid.NewGuid().ToByteArray()};
            yield return new RequestNodeMessageRoutesMessage {TargetNodeIdentity = scaleOutAddress.Identity};
            yield return new UnregisterMessageRouteMessage {ReceiverNodeIdentity = Guid.NewGuid().ToByteArray()};
            yield return new RegisterExternalMessageRouteMessage {NodeIdentity = Guid.NewGuid().ToByteArray()};
            yield return new UnregisterNodeMessage {ReceiverNodeIdentity = Guid.NewGuid().ToByteArray()};
            yield return new DiscoverMessageRouteMessage {RequestorNodeIdentity = Guid.NewGuid().ToByteArray()};
        }

        private static Task Start(Action @delegate)
            => Task.Factory.StartNew(@delegate);

        private RendezvousEndpoint GetCurrentRendezvous()
            => rendezvousEndpoints[currentRendezvousIndex];

        private void SetNextRendezvous()
            => currentRendezvousIndex = rendezvousEndpoints.Length > ++currentRendezvousIndex
                                            ? 0
                                            : currentRendezvousIndex;

        private void SetNewRendezvous(IEnumerable<RendezvousEndpoint> newConfig)
        {
            currentRendezvousIndex = 0;
            rendezvousEndpoints = newConfig.ToArray();
        }
    }
}