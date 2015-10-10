using System;
using System.Linq;
using System.Threading;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Sockets;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMonitorTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private ClusterMonitorSocketFactory clusterMonitorSocketFactory;
        private Mock<ILogger> logger;
        private Mock<ISocketFactory> socketFactory;
        private RouterConfiguration routerConfiguration;
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IRendezvousConfiguration> rendezvousConfiguration;
        private ClusterMembershipConfiguration clusterMembershipConfiguration;

        [SetUp]
        public void Setup()
        {
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            logger = new Mock<ILogger>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateSubscriberSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            rendezvousConfiguration = new Mock<IRendezvousConfiguration>();
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), SocketIdentifier.CreateNew()),
                                      RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateNew())
                                  };
            var rendezvousEndpoint = new RendezvousEndpoints
                                     {
                                         UnicastUri = new Uri("tcp://127.0.0.1:5000"),
                                         MulticastUri = new Uri("tcp://127.0.0.1:5000")
                                     };
            rendezvousConfiguration.Setup(m => m.GetCurrentRendezvousServer()).Returns(rendezvousEndpoint);
            clusterMembership = new Mock<IClusterMembership>();
            clusterMembershipConfiguration = new ClusterMembershipConfiguration
                                             {
                                                 RunAsStandalone = false,
                                                 PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(2),
                                                 PongSilenceBeforeRouteDeletion = TimeSpan.FromMilliseconds(4)
                                             };
        }

        [Test]
        public void TestIfPingIsNotCommingInTime_SwitchToNextRendezvousServer()
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                WaitLongerThanPingSilenceFailover();

                rendezvousConfiguration.Verify(m => m.GetCurrentRendezvousServer(), Times.AtLeastOnce);
                rendezvousConfiguration.Verify(m => m.RotateRendezvousServers(), Times.AtLeastOnce);
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestIfPingComesInTime_SwitchToNextRendezvousServerNeverHappens()
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                WaitLessThanPingSilenceFailover();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
                var ping = new PingMessage
                           {
                               PingId = 1L,
                               PingInterval = TimeSpan.FromSeconds(2)
                           };
                socket.DeliverMessage(Message.Create(ping, PingMessage.MessageIdentity));
                Thread.Sleep(AsyncOp);

                rendezvousConfiguration.Verify(m => m.SetCurrentRendezvousServer(It.IsAny<RendezvousEndpoints>()), Times.Never);
                rendezvousConfiguration.Verify(m => m.RotateRendezvousServers(), Times.Never);
                rendezvousConfiguration.Verify(m => m.GetCurrentRendezvousServer(), Times.Exactly(2));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestIfNonLeaderMessageArrives_NewLeaderIsSelectedFromReceivedMessage()
        {
            clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover = TimeSpan.FromSeconds(10);

            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);

            try
            {
                clusterMonitor.Start();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
                var notLeaderMessage = new RendezvousNotLeaderMessage
                                       {
                                           LeaderMulticastUri = "tpc://127.0.0.2:6000",
                                           LeaderUnicastUri = "tpc://127.0.0.2:6000"
                                       };
                socket.DeliverMessage(Message.Create(notLeaderMessage,
                                                     RendezvousNotLeaderMessage.MessageIdentity));
                Thread.Sleep(AsyncOp);

                rendezvousConfiguration.Verify(m => m.SetCurrentRendezvousServer(It.Is<RendezvousEndpoints>(e => SameServer(e, notLeaderMessage))),
                                               Times.Once());
                rendezvousConfiguration.Verify(m => m.GetCurrentRendezvousServer(), Times.AtLeastOnce);
                rendezvousConfiguration.Verify(m => m.RotateRendezvousServers(), Times.Never);
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestPongMessage_RenewesRegistrationOfSourceNode()
        {
            var sourceNode = new SocketEndpoint(new Uri("tpc://127.0.0.3:7000"), SocketIdentifier.CreateNew());

            clusterMembership.Setup(m => m.KeepAlive(sourceNode)).Returns(true);

            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
                var pong = new PongMessage
                           {
                               PingId = 1L,
                               SocketIdentity = sourceNode.Identity,
                               Uri = sourceNode.Uri.ToSocketAddress()
                           };
                socket.DeliverMessage(Message.Create(pong, PongMessage.MessageIdentity));
                Thread.Sleep(AsyncOp);

                clusterMembership.Verify(m => m.KeepAlive(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == sourceNode.Uri.ToSocketAddress()
                                                                                     && Unsafe.Equals(e.Identity, sourceNode.Identity))),
                                         Times.Once());
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestIfPongMessageComesFromUnknownNode_RequestNodeMessageRoutesMessageSent()
        {
            var sourceNode = new SocketEndpoint(new Uri("tpc://127.0.0.3:7000"), SocketIdentifier.CreateNew());

            clusterMembership.Setup(m => m.KeepAlive(sourceNode)).Returns(false);

            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
                var pong = new PongMessage
                           {
                               PingId = 1L,
                               SocketIdentity = sourceNode.Identity,
                               Uri = sourceNode.Uri.ToSocketAddress()
                           };
                socket.DeliverMessage(Message.Create(pong, PongMessage.MessageIdentity));
                Thread.Sleep(AsyncOp);

                var routesRequestMessage = clusterMonitorSocketFactory
                    .GetClusterMonitorSendingSocket()
                    .GetSentMessages()
                    .BlockingLast(AsyncOp);

                clusterMembership.Verify(m => m.KeepAlive(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == sourceNode.Uri.ToSocketAddress()
                                                                                     && Unsafe.Equals(e.Identity, sourceNode.Identity))),
                                         Times.Once());
                Assert.IsNotNull(routesRequestMessage);
                Assert.IsTrue(Unsafe.Equals(routesRequestMessage.Identity, RequestNodeMessageRoutesMessage.MessageIdentity));
                var payload = routesRequestMessage.GetPayload<RequestNodeMessageRoutesMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.TargetNodeIdentity, sourceNode.Identity));
                Assert.AreEqual(payload.TargetNodeUri, sourceNode.Uri.ToSocketAddress());
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestRequestClusterMessageRoutesMessage_IsForwardedToMessageRouter()
        {
            var payload = new RequestClusterMessageRoutesMessage();
            TestMessageIsForwardedToMessageRouter(payload, RequestClusterMessageRoutesMessage.MessageIdentity);
        }

        [Test]
        public void TestUnregisterMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new UnregisterMessageRouteMessage
                          {
                              Uri = "tcp://127.1.1.1:5000",
                              SocketIdentity = SocketIdentifier.CreateNew()
                          };
            TestMessageIsForwardedToMessageRouter(payload, UnregisterMessageRouteMessage.MessageIdentity);
        }

        [Test]
        public void TestUnregisterNodeMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new UnregisterNodeMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.3:6000",
                              SocketIdentity = SocketIdentifier.CreateNew()
                          };
            TestMessageIsForwardedToMessageRouter(payload, UnregisterNodeMessageRouteMessage.MessageIdentity);
        }

        [Test]
        public void TestRegisterExternalMessageRouteMessage_IsForwardedToMessageRouter()
        {
            var payload = new RegisterExternalMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.3:6000",
                              SocketIdentity = SocketIdentifier.CreateNew()
                          };
            TestMessageIsForwardedToMessageRouter(payload, RegisterExternalMessageRouteMessage.MessageIdentity);
        }

        [Test]
        public void TestUnregisterMessageRouteMessage_DeletesClusterMember()
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
                var message = new UnregisterNodeMessageRouteMessage
                              {
                                  Uri = "tcp://127.0.0.1:5000",
                                  SocketIdentity = SocketIdentifier.CreateNew()
                              };
                socket.DeliverMessage(Message.Create(message, UnregisterNodeMessageRouteMessage.MessageIdentity));
                Thread.Sleep(AsyncOp);

                clusterMembership.Verify(m => m.DeleteClusterMember(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == message.Uri
                                                                                               && Unsafe.Equals(e.Identity, message.SocketIdentity))),
                                         Times.Once());
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestRegisterSelf_SendRegistrationMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
                clusterMonitor.RegisterSelf(new[] {messageIdentifier});

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(Unsafe.Equals(message.Identity, RegisterExternalMessageRouteMessage.MessageIdentity));
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, routerConfiguration.ScaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(payload.MessageContracts.Any(mc => Unsafe.Equals(mc.Identity, messageIdentifier.Identity)
                                                                 && Unsafe.Equals(mc.Version, messageIdentifier.Version)));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestUnregisterSelf_SendUnregisterMessageRouteMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
                clusterMonitor.UnregisterSelf(new[] {messageIdentifier});

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(Unsafe.Equals(message.Identity, UnregisterMessageRouteMessage.MessageIdentity));
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();
                Assert.IsTrue(Unsafe.Equals(payload.SocketIdentity, routerConfiguration.ScaleOutAddress.Identity));
                Assert.AreEqual(payload.Uri, routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress());
                Assert.IsTrue(payload.MessageContracts.Any(mc => Unsafe.Equals(mc.Identity, messageIdentifier.Identity)
                                                                 && Unsafe.Equals(mc.Version, messageIdentifier.Version)));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        [Test]
        public void TestRequestClusterRoutes_SendRequestClusterMessageRoutesMessageToRendezvous()
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                clusterMonitor.RequestClusterRoutes();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
                var message = socket.GetSentMessages().BlockingLast(AsyncOp);

                Assert.IsNotNull(message);
                Assert.IsTrue(Unsafe.Equals(message.Identity, RequestClusterMessageRoutesMessage.MessageIdentity));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        private void TestMessageIsForwardedToMessageRouter<TPayload>(TPayload payload, byte[] messageIdentity)
            where TPayload : IPayload
        {
            var clusterMonitor = new ClusterMonitor(socketFactory.Object,
                                                    routerConfiguration,
                                                    clusterMembership.Object,
                                                    clusterMembershipConfiguration,
                                                    rendezvousConfiguration.Object,
                                                    logger.Object);
            try
            {
                clusterMonitor.Start();

                var socket = clusterMonitorSocketFactory.GetClusterMonitorSubscriptionSocket();
                socket.DeliverMessage(Message.Create(payload, messageIdentity));

                var messageRouterMessage = clusterMonitorSocketFactory
                    .GetRouterCommunicationSocket()
                    .GetSentMessages()
                    .BlockingLast(AsyncOp);

                Assert.IsNotNull(messageRouterMessage);
                Assert.IsTrue(Unsafe.Equals(messageRouterMessage.Identity, messageIdentity));
            }
            finally
            {
                clusterMonitor.Stop();
            }
        }

        private static bool SameServer(RendezvousEndpoints e, RendezvousNotLeaderMessage notLeaderMessage)
        {
            return e.MulticastUri.ToSocketAddress() == notLeaderMessage.LeaderMulticastUri
                   && e.UnicastUri.ToSocketAddress() == notLeaderMessage.LeaderUnicastUri;
        }

        private void WaitLessThanPingSilenceFailover()
        {
            Thread.Sleep((int) (clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover.TotalMilliseconds * 0.5));
        }

        private void WaitLongerThanPingSilenceFailover()
        {
            Thread.Sleep((int) (clusterMembershipConfiguration.PingSilenceBeforeRendezvousFailover.TotalMilliseconds * 1.5));
        }
    }
}