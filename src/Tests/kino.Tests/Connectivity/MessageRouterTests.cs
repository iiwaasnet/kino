using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class MessageRouterTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);
        private RouterConfiguration routerConfiguration;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ILogger> logger;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ISocketFactory> socketFactory;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IClusterMonitorProvider> clusterMonitorProvider;
        private MessageRouterSocketFactory messageRouterSocketFactory;
        private ClusterMembershipConfiguration membershipConfiguration;
        private IEnumerable<IServiceMessageHandler> serviceMessageHandlers;
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private string securityDomain;

        [SetUp]
        public void Setup()
        {
            membershipConfiguration = new ClusterMembershipConfiguration
                                      {
                                          PongSilenceBeforeRouteDeletion = TimeSpan.FromSeconds(10)
                                      };
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateIdentity()),
                                      RouterAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateIdentity())
                                  };
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            clusterMembership = new Mock<IClusterMembership>();
            messageRouterSocketFactory = new MessageRouterSocketFactory(routerConfiguration);
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterMonitorProvider = new Mock<IClusterMonitorProvider>();
            clusterMonitorProvider.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(messageRouterSocketFactory.CreateSocket);
            socketFactory.Setup(m => m.GetSocketDefaultConfiguration()).Returns(new SocketConfiguration());
            logger = new Mock<ILogger>();
            serviceMessageHandlers = Enumerable.Empty<IServiceMessageHandler>();
            securityDomain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.SecurityDomainIsAllowed(securityDomain)).Returns(true);
            securityProvider.Setup(m => m.GetAllowedSecurityDomains()).Returns(new[] {securityDomain});
            securityProvider.Setup(m => m.GetSecurityDomain(It.IsAny<byte[]>())).Returns(securityDomain);
        }

        [Test]
        public void MessageRouterUponStart_CreatesRouterLocalAndTwoScaleoutSockets()
        {
            var router = CreateMessageRouter();
            try
            {
                StartMessageRouter(router);

                socketFactory.Verify(m => m.CreateRouterSocket(), Times.Exactly(3));
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void RegisterLocalMessageHandlers_AddsActorIdentifier()
        {
            var internalRoutingTable = new InternalRoutingTable();
            serviceMessageHandlers = new[]
                                     {
                                         new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
                                                                                     internalRoutingTable,
                                                                                     securityProvider.Object,
                                                                                     logger.Object)
                                     };

            var router = CreateMessageRouter(internalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentity = Guid.NewGuid().ToByteArray();
                var version = Guid.NewGuid().ToByteArray();
                var socketIdentity = Guid.NewGuid().ToByteArray();
                var partition = Guid.NewGuid().ToByteArray();
                var message = Message.Create(new RegisterInternalMessageRouteMessage
                                             {
                                                 SocketIdentity = socketIdentity,
                                                 LocalMessageContracts = new[]
                                                                         {
                                                                             new MessageContract
                                                                             {
                                                                                 Identity = messageIdentity,
                                                                                 Version = version,
                                                                                 Partition = partition
                                                                             }
                                                                         }
                                             });
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, partition));

                Assert.IsNotNull(identifier);
                Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
                CollectionAssert.AreEqual(socketIdentity, identifier.Identity);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void LocalActorRegistrations_AreNotBroadcastedToOtherNodes()
        {
            var internalRoutingTable = new InternalRoutingTable();
            serviceMessageHandlers = new[]
                                     {
                                         new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
                                                                                     internalRoutingTable,
                                                                                     securityProvider.Object,
                                                                                     logger.Object)
                                     };

            var router = CreateMessageRouter(internalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentity = Guid.NewGuid().ToByteArray();
                var version = Guid.NewGuid().ToByteArray();
                var socketIdentity = Guid.NewGuid().ToByteArray();
                var partition = Guid.NewGuid().ToByteArray();
                var securityDomain = Guid.NewGuid().ToString();
                var message = Message.Create(new RegisterInternalMessageRouteMessage
                                             {
                                                 SocketIdentity = socketIdentity,
                                                 LocalMessageContracts = new[]
                                                                         {
                                                                             new MessageContract
                                                                             {
                                                                                 Identity = messageIdentity,
                                                                                 Version = version,
                                                                                 Partition = partition
                                                                             }
                                                                         }
                                             },
                                             securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, partition));

                Assert.IsNotNull(identifier);

                clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), securityDomain), Times.Never);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void RegisterGlobalMessageHandlers_AddsActorIdentifier()
        {
            var internalRoutingTable = new InternalRoutingTable();
            serviceMessageHandlers = new[]
                                     {
                                         new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
                                                                                     internalRoutingTable,
                                                                                     securityProvider.Object,
                                                                                     logger.Object)
                                     };

            var router = CreateMessageRouter(internalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentity = Guid.NewGuid().ToByteArray();
                var version = Guid.NewGuid().ToByteArray();
                var socketIdentity = Guid.NewGuid().ToByteArray();
                var partition = Guid.NewGuid().ToByteArray();
                var message = Message.Create(new RegisterInternalMessageRouteMessage
                                             {
                                                 SocketIdentity = socketIdentity,
                                                 GlobalMessageContracts = new[]
                                                                          {
                                                                              new MessageContract
                                                                              {
                                                                                  Identity = messageIdentity,
                                                                                  Version = version,
                                                                                  Partition = partition
                                                                              }
                                                                          }
                                             });
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, partition));

                Assert.IsNotNull(identifier);
                Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
                CollectionAssert.AreEqual(socketIdentity, identifier.Identity);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void GlobalActorRegistrations_AreBroadcastedToOtherNodes()
        {
            TestGlobalActorRegistrationsBroadcast(new[] {securityDomain}, Times.Once());
        }

        [Test]
        public void IfNoAllowedSecurityDomainsConfigured_GlobalActorRegistrationsAreNotBroadcastedToOtherNodes()
        {
            TestGlobalActorRegistrationsBroadcast(Enumerable.Empty<string>(), Times.Never());
        }

        private void TestGlobalActorRegistrationsBroadcast(IEnumerable<string> allowedDomains, Times times)
        {
            securityProvider.Setup(m => m.GetAllowedSecurityDomains()).Returns(allowedDomains);
            var internalRoutingTable = new InternalRoutingTable();
            serviceMessageHandlers = new[]
                                     {
                                         new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
                                                                                     internalRoutingTable,
                                                                                     securityProvider.Object,
                                                                                     logger.Object)
                                     };

            var router = CreateMessageRouter(internalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentity = Guid.NewGuid().ToByteArray();
                var version = Guid.NewGuid().ToByteArray();
                var socketIdentity = Guid.NewGuid().ToByteArray();
                var message = Message.Create(new RegisterInternalMessageRouteMessage
                                             {
                                                 SocketIdentity = socketIdentity,
                                                 GlobalMessageContracts = new[]
                                                                          {
                                                                              new MessageContract
                                                                              {
                                                                                  Identity = messageIdentity,
                                                                                  Version = version
                                                                              }
                                                                          }
                                             },
                                             securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, IdentityExtensions.Empty));

                Assert.IsNotNull(identifier);
                Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
                CollectionAssert.AreEqual(socketIdentity, identifier.Identity);

                var messageIdentifiers = new[] {new MessageIdentifier(version, messageIdentity, IdentityExtensions.Empty)};
                clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), It.IsAny<string>()), times);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void HandlerForReceiverIdentity_HasHighestPriority()
        {
            var internalRoutingTable = new Mock<IInternalRoutingTable>();

            var router = CreateMessageRouter(internalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = (Message) SendMessageOverMessageHub();

                var callbackSocketIdentity = message.CallbackReceiverIdentity;
                var callbackIdentifier = new MessageIdentifier(IdentityExtensions.Empty, callbackSocketIdentity, IdentityExtensions.Empty);
                internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(callbackIdentifier))))
                                    .Returns(new SocketIdentifier(callbackSocketIdentity));

                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(callbackIdentifier))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void MessageIsRouted_BasedOnHandlerIdentities()
        {
            var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
            var partition = Guid.NewGuid().ToByteArray();
            var actorIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                                .Returns(actorSocketIdentity);

            var router = CreateMessageRouter(internalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage {Partition = partition});
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void MessageFromOnePartition_IsNotRoutedToActorFromOtherPartition()
        {
            var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
            var actorPartition = Guid.NewGuid().ToByteArray();
            var actorIdentifier = MessageIdentifier.Create<SimpleMessage>(actorPartition);
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                                .Returns(actorSocketIdentity);

            var router = CreateMessageRouter(internalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var messagePartition = Guid.NewGuid().ToByteArray();
                var message = Message.Create(new SimpleMessage {Partition = messagePartition});
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Never);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void MessageIsRouted_BasedOnHandler_IdentityVersionPartition()
        {
            var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
            var partition = Guid.NewGuid().ToByteArray();
            var actorIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                                .Returns(actorSocketIdentity);

            var router = CreateMessageRouter(internalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage {Partition = partition});
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfLocalRoutingTableHasNoMessageHandlerRegistration_MessageRoutedToOtherNodes()
        {
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>(), null))
                                .Returns(new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())});

            var router = CreateMessageRouter(null, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                var messageOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(message, messageOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfReceivingNodeIsSet_MessageAlwaysRoutedToOtherNodes()
        {
            var externalNode = new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())};
            var message = Message.Create(new SimpleMessage(), securityDomain);
            message.SetReceiverNode(new SocketIdentifier(externalNode.Node.SocketIdentity));

            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m =>
                                       m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi)), It.Is<byte[]>(id => Unsafe.Equals(id, externalNode.Node.SocketIdentity))))
                                .Returns(externalNode);
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi))))
                                .Returns(SocketIdentifier.Create());

            var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                var messageOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(message, messageOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfReceivingNodeIsSetAndExternalRouteIsNotRegistered_RouterRequestsDiscovery()
        {
            var externalNode = new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())};
            var message = Message.Create(new SimpleMessage());
            message.SetReceiverNode(new SocketIdentifier(externalNode.Node.SocketIdentity));

            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>(), It.IsAny<byte[]>()))
                                .Returns((PeerConnection) null);
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi))))
                                .Returns(SocketIdentifier.Create());

            var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                clusterMonitor.Verify(m => m.UnregisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>()), Times.Never());
                clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => message.Equals(id))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void MessageHopIsAdded_WhenMessageIsSentToOtherNode()
        {
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>(), null))
                                .Returns(new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())});

            var router = CreateMessageRouter(null, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = Message.CreateFlowStartMessage(new SimpleMessage(), securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);
                Assert.AreEqual(0, message.Hops);

                var messageOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(1, messageOut.Hops);
                Assert.IsTrue(Unsafe.Equals(message.CorrelationId, messageOut.CorrelationId));
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void BroadcastMessage_IsRoutedToAllLocalAndRemoteActors()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
                                .Returns(new[] {new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())}});
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
                                .Returns(new[] {new SocketIdentifier(Guid.NewGuid().ToByteArray())});

            var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), securityDomain, DistributionPattern.Broadcast);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                var messageScaleOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
                var messageLocalOut = messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(message, messageScaleOut);
                Assert.AreEqual(message, messageLocalOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void BroadcastMessageIsRoutedOnlyToLocalActors_IfHopsCountGreaterThanZero()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
                                .Returns(new[] {new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())}});
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
                                .Returns(new[] {new SocketIdentifier(Guid.NewGuid().ToByteArray())});

            var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = (Message) Message.Create(new SimpleMessage(), DistributionPattern.Broadcast);
                message.AddHop();
                Assert.AreEqual(1, message.Hops);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));
                var messageLocalOut = messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(message, messageLocalOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void BroadcastMessage_IsRoutedToRemoteActorsEvenIfNoLocalActorsRegistered()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
                                .Returns(new[] {new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())}});
            var internalRoutingTable = new Mock<IInternalRoutingTable>();

            var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), securityDomain, DistributionPattern.Broadcast);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                var messageScaleOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
                Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));

                Assert.AreEqual(message, messageScaleOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void BroadcastMessageIsNotRoutedAndRouteDiscoverRequestSent_IfNoLocalActorsRegisteredAndHopsCountGreaterThanZero()
        {
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            var internalRoutingTable = new Mock<IInternalRoutingTable>();

            var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);

                var message = (Message) Message.Create(new SimpleMessage(), DistributionPattern.Broadcast);
                message.AddHop();
                Assert.AreEqual(1, message.Hops);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));
                Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));

                clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => id.Equals(MessageIdentifier.Create<SimpleMessage>()))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void PeerNodeIsConnected_WhenMessageIsForwardedToIt()
        {
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity()), Connected = false};
            externalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier)), null)).Returns(peerConnection);

            var router = CreateMessageRouter(null, externalRoutingTable.Object);
            try
            {
                StartMessageRouter(router);
                Assert.IsFalse(peerConnection.Connected);
                var socket = messageRouterSocketFactory.GetScaleoutBackendSocket();
                Assert.IsFalse(socket.IsConnected());
                var message = Message.Create(new SimpleMessage(), securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                var messageOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.IsTrue(peerConnection.Connected);
                Assert.IsTrue(socket.IsConnected());
                Assert.AreEqual(message, messageOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void MessageReceivedFromOtherNode_ForwardedToLocalRouterSocket()
        {
            var messageSignature = Guid.NewGuid().ToByteArray();
            TestMessageReceivedFromOtherNode(messageSignature, messageSignature, MessageIdentifier.Create<SimpleMessage>());
        }

        [Test]
        public void MessageReceivedFromOtherNodeNotForwardedToLocalRouterSocket_IfMessageSignatureDoesntMatch()
        {
            TestMessageReceivedFromOtherNode(Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray(), KinoMessages.Exception);
        }

        private void TestMessageReceivedFromOtherNode(byte[] messageSignature, byte[] generatedSignature, MessageIdentifier expected)
        {
            var router = CreateMessageRouter();
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), securityDomain);
                securityProvider.Setup(m => m.CreateSignature(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(messageSignature);
                message.As<Message>().SignMessage(securityProvider.Object);

                securityProvider.Setup(m => m.CreateSignature(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(generatedSignature);
                messageRouterSocketFactory.GetScaleoutFrontendSocket().DeliverMessage(message);

                var messageOut = messageRouterSocketFactory.GetScaleoutFrontendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(expected, new MessageIdentifier(messageOut));
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfUnhandledMessageReceivedFromOtherNode_RouterUnregistersSelfAndRequestsDiscovery()
        {
            var router = CreateMessageRouter();
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                var message = (Message) Message.Create(new SimpleMessage());
                message.AddHop();
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                clusterMonitor.Verify(m => m.UnregisterSelf(It.Is<IEnumerable<MessageIdentifier>>(ids => ids.First().Equals(messageIdentifier))), Times.Once());
                clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => id.Equals(messageIdentifier))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfUnhandledMessageReceivedFromLocalActor_RouterRequestsDiscovery()
        {
            var router = CreateMessageRouter();
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                var message = Message.Create(new SimpleMessage());
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                clusterMonitor.Verify(m => m.UnregisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>()), Times.Never());
                clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => id.Equals(messageIdentifier))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfMessageRouterCannotHandleMessage_SelfRegisterIsNotCalled()
        {
            var internalRoutingTable = new InternalRoutingTable();
            var router = CreateMessageRouter(internalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                var securityDomain = Guid.NewGuid().ToString();
                var message = Message.Create(new DiscoverMessageRouteMessage
                                             {
                                                 MessageContract = new MessageContract
                                                                   {
                                                                       Version = messageIdentifier.Version,
                                                                       Identity = messageIdentifier.Identity,
                                                                       Partition = messageIdentifier.Partition
                                                                   }
                                             },
                                             securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsFalse(internalRoutingTable.CanRouteMessage(messageIdentifier));
                clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), securityDomain), Times.Never());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfMessageRouterCanHandleMessageBeingDiscovered_SelfRegisterIsCalled()
        {
            var internalRoutingTable = new InternalRoutingTable();
            serviceMessageHandlers = new[]
                                     {
                                         new MessageRouteDiscoveryHandler(clusterMonitorProvider.Object,
                                                                          internalRoutingTable,
                                                                          securityProvider.Object,
                                                                          logger.Object)
                                     };

            var router = CreateMessageRouter(internalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                internalRoutingTable.AddMessageRoute(messageIdentifier, SocketIdentifier.Create());
                var message = Message.Create(new DiscoverMessageRouteMessage
                                             {
                                                 MessageContract = new MessageContract
                                                                   {
                                                                       Version = messageIdentifier.Version,
                                                                       Identity = messageIdentifier.Identity,
                                                                       Partition = messageIdentifier.Partition
                                                                   }
                                             },
                                             securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsTrue(internalRoutingTable.CanRouteMessage(messageIdentifier));
                clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageIdentifier>>(ids => ids.First().Equals(messageIdentifier)),
                                                          securityDomain),
                                      Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfRegisterExternalMessageRouteMessageReceived_AllRoutesAreAddedToExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var config = new RouterConfiguration {DeferPeerConnection = true};
            serviceMessageHandlers = new[]
                                     {
                                         new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
                                                                                     clusterMembership.Object,
                                                                                     config,
                                                                                     securityProvider.Object,
                                                                                     logger.Object)
                                     };

            var router = CreateMessageRouter(null, externalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             MessageIdentifier.Create<SimpleMessage>(),
                                             MessageIdentifier.Create<AsyncMessage>()
                                         };
                var socketIdentity = SocketIdentifier.CreateIdentity();
                var message = Message.Create(new RegisterExternalMessageRouteMessage
                                             {
                                                 Uri = "tcp://127.0.0.1:8000",
                                                 SocketIdentity = socketIdentity,
                                                 MessageContracts = messageIdentifiers.Select(mi => new MessageContract
                                                                                                    {
                                                                                                        Version = mi.Version,
                                                                                                        Identity = mi.Identity,
                                                                                                        Partition = mi.Partition
                                                                                                    }).ToArray()
                                             },
                                             securityDomain);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsTrue(Unsafe.Equals(socketIdentity, externalRoutingTable.FindRoute(messageIdentifiers.First(), null).Node.SocketIdentity));
                Assert.IsTrue(Unsafe.Equals(socketIdentity, externalRoutingTable.FindRoute(messageIdentifiers.Second(), null).Node.SocketIdentity));
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfUnregisterMessageRouteMessage_RoutesAreRemovedFromExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            serviceMessageHandlers = new[]
                                     {
                                         new MessageRouteUnregistrationHandler(externalRoutingTable,
                                                                               securityProvider.Object,
                                                                               logger.Object)
                                     };

            var router = CreateMessageRouter(null, externalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             MessageIdentifier.Create<SimpleMessage>(),
                                             MessageIdentifier.Create<AsyncMessage>()
                                         };

                var socketIdentity = SocketIdentifier.Create();
                var uri = new Uri("tcp://127.0.0.1:8000");
                messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = uri.ToSocketAddress(),
                                                 SocketIdentity = socketIdentity.Identity,
                                                 MessageContracts = messageIdentifiers.Select(mi => new MessageContract
                                                                                                    {
                                                                                                        Version = mi.Version,
                                                                                                        Identity = mi.Identity,
                                                                                                        Partition = mi.Partition
                                                                                                    }).ToArray()
                                             },
                                             securityDomain);

                CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                CollectionAssert.IsEmpty(externalRoutingTable.GetAllRoutes());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void UnregisterMessageRouteMessageForAllowedDomain_DeletesClusterMember()
        {
            TestUnregisterMessageRouteMessage(securityDomain, Times.Once());
        }

        [Test]
        public void UnregisterMessageRouteMessageForNotAllowedDomain_DoesntDeletesClusterMember()
        {
            TestUnregisterMessageRouteMessage(Guid.NewGuid().ToString(), Times.Never());
        }

        private void TestUnregisterMessageRouteMessage(string securityDomain, Times times)
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            serviceMessageHandlers = new[]
                                     {
                                         new NodeUnregistrationHandler(externalRoutingTable,
                                                                       clusterMembership.Object,
                                                                       securityProvider.Object)
                                     };

            var router = CreateMessageRouter(null, externalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var socket = messageRouterSocketFactory.GetRouterSocket();
                var message = new UnregisterNodeMessage
                              {
                                  Uri = "tcp://127.0.0.1:5000",
                                  SocketIdentity = SocketIdentifier.CreateIdentity()
                              };
                socket.DeliverMessage(Message.Create(message, securityDomain));
                Thread.Sleep(AsyncOp);

                clusterMembership.Verify(m => m.DeleteClusterMember(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == message.Uri
                                                                                               && Unsafe.Equals(e.Identity, message.SocketIdentity))),
                                         times);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfAllMessageRoutesUnregisteredForNode_SocketIsDisconnected()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            serviceMessageHandlers = new[]
                                     {
                                         new MessageRouteUnregistrationHandler(externalRoutingTable,
                                                                               securityProvider.Object,
                                                                               logger.Object)
                                     };

            var router = CreateMessageRouter(null, externalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             MessageIdentifier.Create<SimpleMessage>(),
                                             MessageIdentifier.Create<AsyncMessage>()
                                         };

                var socketIdentity = SocketIdentifier.Create();
                var uri = new Uri("tcp://127.0.0.1:8000");
                messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
                var peerConnection = externalRoutingTable.FindRoute(messageIdentifiers.First(), null);
                peerConnection.Connected = true;
                var backEndScoket = messageRouterSocketFactory.GetScaleoutBackendSocket();
                backEndScoket.Connect(uri);
                Assert.IsTrue(backEndScoket.IsConnected());
                var message = Message.Create(new UnregisterMessageRouteMessage
                                             {
                                                 Uri = uri.ToSocketAddress(),
                                                 SocketIdentity = socketIdentity.Identity,
                                                 MessageContracts = messageIdentifiers.Select(mi => new MessageContract
                                                                                                    {
                                                                                                        Version = mi.Version,
                                                                                                        Identity = mi.Identity,
                                                                                                        Partition = mi.Partition
                                                                                                    }).ToArray()
                                             },
                                             securityDomain);

                CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsFalse(backEndScoket.IsConnected());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfUnregisterNodeMessageRouteMessageAndConnectionWasEstablished_SocketIsDisconnected()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            serviceMessageHandlers = new[]
                                     {
                                         new NodeUnregistrationHandler(externalRoutingTable,
                                                                       clusterMembership.Object,
                                                                       securityProvider.Object)
                                     };

            var router = CreateMessageRouter(null, externalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                var socketIdentity = SocketIdentifier.Create();
                var uri = new Uri("tcp://127.0.0.1:8000");
                externalRoutingTable.AddMessageRoute(messageIdentifier, socketIdentity, uri);
                var peerConnection = externalRoutingTable.FindRoute(messageIdentifier, null);
                peerConnection.Connected = true;
                var backEndScoket = messageRouterSocketFactory.GetScaleoutBackendSocket();
                backEndScoket.Connect(uri);
                Assert.IsTrue(backEndScoket.IsConnected());
                var message = Message.Create(new UnregisterNodeMessage
                                             {
                                                 Uri = uri.ToSocketAddress(),
                                                 SocketIdentity = socketIdentity.Identity
                                             });

                CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsFalse(backEndScoket.IsConnected());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfUnregisterNodeMessageRouteMessage_AllRoutesAreRemovedFromExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            serviceMessageHandlers = new[]
                                     {
                                         new NodeUnregistrationHandler(externalRoutingTable,
                                                                       clusterMembership.Object,
                                                                       securityProvider.Object)
                                     };

            var router = CreateMessageRouter(null, externalRoutingTable);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             MessageIdentifier.Create<SimpleMessage>(),
                                             MessageIdentifier.Create<AsyncMessage>()
                                         };

                var socketIdentity = SocketIdentifier.Create();
                var uri = new Uri("tcp://127.0.0.1:8000");
                messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
                var message = Message.Create(new UnregisterNodeMessage
                                             {
                                                 Uri = uri.ToSocketAddress(),
                                                 SocketIdentity = socketIdentity.Identity
                                             });

                CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                CollectionAssert.IsEmpty(externalRoutingTable.GetAllRoutes());
            }
            finally
            {
                router.Stop();
            }
        }

        private IMessage SendMessageOverMessageHub()
        {
            var performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            var logger = new Mock<ILogger>();
            var socketFactory = new Mock<ISocketFactory>();
            var socket = new StubSocket();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            var callback = CallbackPoint.Create<SimpleMessage>();

            var messageHub = new MessageHub(socketFactory.Object,
                                            new CallbackHandlerStack(),
                                            new MessageHubConfiguration(),
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger.Object);
            messageHub.Start();
            messageHub.EnqueueRequest(message, callback);
            Thread.Sleep(AsyncOpCompletionDelay);

            return socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        }

        private MessageRouter CreateMessageRouter(IInternalRoutingTable internalRoutingTable = null,
                                                  IExternalRoutingTable externalRoutingTable = null)
            => new MessageRouter(socketFactory.Object,
                                 internalRoutingTable ?? new InternalRoutingTable(),
                                 externalRoutingTable ?? new ExternalRoutingTable(logger.Object),
                                 routerConfiguration,
                                 clusterMonitorProvider.Object,
                                 serviceMessageHandlers,
                                 membershipConfiguration,
                                 performanceCounterManager.Object,
                                 securityProvider.Object,
                                 logger.Object);

        private static void StartMessageRouter(IMessageRouter messageRouter)
        {
            messageRouter.Start(StartTimeout);
            Thread.Sleep(AsyncOpCompletionDelay);
        }
    }
}