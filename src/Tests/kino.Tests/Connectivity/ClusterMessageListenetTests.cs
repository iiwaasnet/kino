using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMessageListenetTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private ClusterMonitorSocketFactory clusterMonitorSocketFactory;
        private Mock<ILogger> logger;
        private Mock<ISocketFactory> socketFactory;
        private RouterConfiguration routerConfiguration;
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private Mock<IClusterMessageSender> clusterMessageSender;
        private ClusterMembershipConfiguration clusterMembershipConfiguration;

        [SetUp]
        public void Setup()
        {
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            clusterMessageSender = new Mock<IClusterMessageSender>();
            logger = new Mock<ILogger>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            rendezvousCluster = new Mock<IRendezvousCluster>();
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), SocketIdentifier.CreateIdentity()),
                                      RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateIdentity())
                                  };
            var rendezvousEndpoint = new RendezvousEndpoint(new Uri("tcp://127.0.0.1:5000"),
                                                            new Uri("tcp://127.0.0.1:5000"));
            rendezvousCluster.Setup(m => m.GetCurrentRendezvousServer()).Returns(rendezvousEndpoint);
            clusterMembership = new Mock<IClusterMembership>();
            clusterMembershipConfiguration = new ClusterMembershipConfiguration
                                             {
                                                 RunAsStandalone = false,
                                                 PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(4),
                                                 PongSilenceBeforeRouteDeletion = TimeSpan.FromMilliseconds(8)
                                             };
        }

        [Test]
        public void TestIfPingIsNotCommingInTime_SwitchToNextRendezvousServer()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            Thread.Sleep(WaitLongerThanPingSilenceFailover());
            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.GetCurrentRendezvousServer(), Times.AtLeastOnce);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.AtLeastOnce);
        }

        [Test]
        public void TestIfPingComesInTime_SwitchToNextRendezvousServerNeverHappens()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var ping = new PingMessage
                       {
                           PingId = 1L,
                           PingInterval = TimeSpan.FromSeconds(2)
                       };

            Thread.Sleep(WaitLessThanPingSilenceFailover());
            socket.DeliverMessage(Message.Create(ping));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.SetCurrentRendezvousServer(It.IsAny<RendezvousEndpoint>()), Times.Never);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.Never);
            rendezvousCluster.Verify(m => m.GetCurrentRendezvousServer(), Times.Once());
        }

        [Test]
        public void TestIfNonLeaderMessageArrives_NewLeaderIsSelectedFromReceivedMessage()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var notLeaderMessage = new RendezvousNotLeaderMessage
                                   {
                                       NewLeader = new RendezvousNode
                                                   {
                                                       MulticastUri = "tpc://127.0.0.2:6000",
                                                       UnicastUri = "tpc://127.0.0.2:6000"
                                                   }
                                   };
            socket.DeliverMessage(Message.Create(notLeaderMessage));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.SetCurrentRendezvousServer(It.Is<RendezvousEndpoint>(e => SameServer(e, notLeaderMessage))),
                                     Times.Once());
            rendezvousCluster.Verify(m => m.GetCurrentRendezvousServer(), Times.AtLeastOnce);
            rendezvousCluster.Verify(m => m.RotateRendezvousServers(), Times.Never);
        }

        [Test]
        public void TestPongMessage_RenewesRegistrationOfSourceNode()
        {
            var sourceNode = new SocketEndpoint(new Uri("tpc://127.0.0.3:7000"), SocketIdentifier.CreateIdentity());

            clusterMembership.Setup(m => m.KeepAlive(sourceNode)).Returns(true);

            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var pong = new PongMessage
                       {
                           PingId = 1L,
                           SocketIdentity = sourceNode.Identity,
                           Uri = sourceNode.Uri.ToSocketAddress()
                       };
            socket.DeliverMessage(Message.Create(pong));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            clusterMembership.Verify(m => m.KeepAlive(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == sourceNode.Uri.ToSocketAddress()
                                                                                 && Unsafe.Equals(e.Identity, sourceNode.Identity))),
                                     Times.Once());
        }

        [Test]
        public void TestIfPongMessageComesFromUnknownNode_RequestNodeMessageRoutesMessageSent()
        {
            var sourceNode = new SocketEndpoint(new Uri("tpc://127.0.0.3:7000"), SocketIdentifier.CreateIdentity());

            clusterMembership.Setup(m => m.KeepAlive(sourceNode)).Returns(false);

            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var pong = new PongMessage
                       {
                           PingId = 1L,
                           SocketIdentity = sourceNode.Identity,
                           Uri = sourceNode.Uri.ToSocketAddress()
                       };
            var routesRequestMessage = new RequestNodeMessageRoutesMessage
                                       {
                                           TargetNodeIdentity = pong.SocketIdentity,
                                           TargetNodeUri = pong.Uri
                                       };
            socket.DeliverMessage(Message.Create(pong));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            clusterMembership.Verify(m => m.KeepAlive(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == sourceNode.Uri.ToSocketAddress()
                                                                                 && Unsafe.Equals(e.Identity, sourceNode.Identity))),
                                     Times.Once());
            clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => RoutesRequestMessage(msg, routesRequestMessage))), Times.Once);
            Assert.IsNotNull(routesRequestMessage);
        }

        [Test]
        public void TestUnregisterMessageRouteMessage_DeletesClusterMember()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            var message = new UnregisterNodeMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.1:5000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            socket.DeliverMessage(Message.Create(message));
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            clusterMembership.Verify(m => m.DeleteClusterMember(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == message.Uri
                                                                                           && Unsafe.Equals(e.Identity, message.SocketIdentity))),
                                     Times.Once());
        }

        [Test]
        public void TestIfRendezvousReconfigurationMessageArrives_RendezvousClusterIsChanged()
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var newRendezouvEndpoint = new RendezvousEndpoint(new Uri("tcp://192.0.0.1:8000"),
                                                              new Uri("tcp://192.0.0.1:8001"));
            var message = Message.Create(new RendezvousConfigurationChangedMessage
                                         {
                                             RendezvousNodes = new[]
                                                               {
                                                                   new RendezvousNode
                                                                   {
                                                                       UnicastUri = newRendezouvEndpoint.UnicastUri.AbsoluteUri,
                                                                       MulticastUri = newRendezouvEndpoint.MulticastUri.AbsoluteUri
                                                                   }
                                                               }
                                         });

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            socket.DeliverMessage(message);
            Thread.Sleep(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            rendezvousCluster.Verify(m => m.Reconfigure(It.Is<IEnumerable<RendezvousEndpoint>>(ep => ep.Contains(newRendezouvEndpoint))),
                                     Times.Once);
        }

        [Test]
        public void TestRequestClusterMessageRoutesMessage_IsForwardedToMessageRouter()
        {
            var payload = new RequestClusterMessageRoutesMessage();
            TestMessageIsForwardedToMessageRouter(payload, KinoMessages.RequestClusterMessageRoutes.Identity);
        }

        [Test]
        public void TestUnregisterMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new UnregisterMessageRouteMessage
                          {
                              Uri = "tcp://127.1.1.1:5000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            TestMessageIsForwardedToMessageRouter(payload, KinoMessages.UnregisterMessageRoute.Identity);
        }

        [Test]
        public void TestUnregisterNodeMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new UnregisterNodeMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.3:6000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            TestMessageIsForwardedToMessageRouter(payload, KinoMessages.UnregisterNodeMessageRoute.Identity);
        }

        [Test]
        public void TestDiscoverMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new DiscoverMessageRouteMessage
                          {
                              RequestorUri = "tcp://127.0.0.3:6000",
                              RequestorSocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            TestMessageIsForwardedToMessageRouter(payload, KinoMessages.DiscoverMessageRoute.Identity);
        }

        [Test]
        public void TestRegisterExternalMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new RegisterExternalMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.3:6000",
                              SocketIdentity = SocketIdentifier.CreateIdentity()
                          };
            TestMessageIsForwardedToMessageRouter(payload, KinoMessages.RegisterExternalMessageRoute.Identity);
        }

        private void TestMessageIsForwardedToMessageRouter<TPayload>(TPayload payload, byte[] messageIdentity)
            where TPayload : IPayload
        {
            var clusterMessageListener = new ClusterMessageListener(rendezvousCluster.Object,
                                                                    socketFactory.Object,
                                                                    routerConfiguration,
                                                                    clusterMessageSender.Object,
                                                                    clusterMembership.Object,
                                                                    clusterMembershipConfiguration,
                                                                    logger.Object);

            var cancellationSource = new CancellationTokenSource();
            var task = StartListeningMessages(clusterMessageListener, cancellationSource.Token);

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
            socket.DeliverMessage(Message.Create(payload));

            var messageRouterMessage = clusterMonitorSocketFactory
                .GetRouterCommunicationSocket()
                .GetSentMessages()
                .BlockingLast(AsyncOp);

            cancellationSource.Cancel();
            task.Wait();

            Assert.IsNotNull(messageRouterMessage);
            Assert.IsTrue(Unsafe.Equals(messageRouterMessage.Identity, messageIdentity));
        }

        private bool RoutesRequestMessage(IMessage message, RequestNodeMessageRoutesMessage routesRequestMessage)
        {
            var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();
            return Unsafe.Equals(payload.Identity, routesRequestMessage.Identity)
                   && payload.TargetNodeUri == routesRequestMessage.TargetNodeUri;
        }

        private TimeSpan WaitLessThanPingSilenceFailover()
            => TimeSpan.FromMilliseconds(clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover.TotalMilliseconds * 0.5);

        private TimeSpan WaitLongerThanPingSilenceFailover()
            =>
                TimeSpan.FromMilliseconds(clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover.TotalMilliseconds * 1.5);

        private static bool SameServer(RendezvousEndpoint e, RendezvousNotLeaderMessage notLeaderMessage)
            => e.MulticastUri.ToSocketAddress() == notLeaderMessage.NewLeader.MulticastUri
               && e.UnicastUri.ToSocketAddress() == notLeaderMessage.NewLeader.UnicastUri;

        private Task StartListeningMessages(IClusterMessageListener clusterMessageListener, Action restartRequestAction, CancellationToken token)
            => Task.Factory.StartNew(() => clusterMessageListener.StartBlockingListenMessages(restartRequestAction, token, new Barrier(1)),
                                     TaskCreationOptions.LongRunning);

        private Task StartListeningMessages(IClusterMessageListener clusterMessageListener, CancellationToken token)
            => StartListeningMessages(clusterMessageListener, () => { }, token);
    }
}