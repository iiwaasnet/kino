using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
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
        private ILogger logger;
        private Mock<ISocketFactory> socketFactory;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IClusterMonitorProvider> clusterMonitorProvider;
        private MessageRouterSocketFactory messageRouterSocketFactory;
        private ClusterMembershipConfiguration membershipConfiguration;
        private IEnumerable<IServiceMessageHandler> serviceMessageHandlers;
        private Mock<IClusterMembership> clusterMembership;

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
            clusterMembership = new Mock<IClusterMembership>();
            messageRouterSocketFactory = new MessageRouterSocketFactory(routerConfiguration);
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterMonitorProvider = new Mock<IClusterMonitorProvider>();
            clusterMonitorProvider.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(messageRouterSocketFactory.CreateSocket);
            socketFactory.Setup(m => m.GetSocketDefaultConfiguration()).Returns(new SocketConfiguration());
            logger = new Mock<ILogger>().Object;
            serviceMessageHandlers = Enumerable.Empty<IServiceMessageHandler>();
        }

        [Test]
        public void MessageRouterUponStart_CreatesRouterLocalAndTwoScaleoutSockets()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
            serviceMessageHandlers = new[] {new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object, internalRoutingTable, logger)};

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
            serviceMessageHandlers = new[] {new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object, internalRoutingTable, logger)};

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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

                clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>()), Times.Never);
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
            serviceMessageHandlers = new[] {new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object, internalRoutingTable, logger)};

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
            var internalRoutingTable = new InternalRoutingTable();
            serviceMessageHandlers = new[] {new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object, internalRoutingTable, logger)};

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
                                             });
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, IdentityExtensions.Empty));

                Assert.IsNotNull(identifier);
                Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
                CollectionAssert.AreEqual(socketIdentity, identifier.Identity);

                var messageIdentifiers = new[] {new MessageIdentifier(version, messageIdentity, IdentityExtensions.Empty)};
                clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageIdentifier>>(handlers => messageIdentifiers.SequenceEqual(handlers))), Times.Once);
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

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
            var messageHandlerStack = new Mock<IInternalRoutingTable>();
            messageHandlerStack.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                               .Returns(actorSocketIdentity);

            var router = new MessageRouter(socketFactory.Object,
                                           messageHandlerStack.Object,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage {Partition = partition});
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                messageHandlerStack.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
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
            var messageHandlerStack = new Mock<IInternalRoutingTable>();
            messageHandlerStack.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                               .Returns(actorSocketIdentity);

            var router = new MessageRouter(socketFactory.Object,
                                           messageHandlerStack.Object,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messagePartition = Guid.NewGuid().ToByteArray();
                var message = Message.Create(new SimpleMessage {Partition = messagePartition});
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                messageHandlerStack.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Never);
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
            var messageHandlerStack = new Mock<IInternalRoutingTable>();
            messageHandlerStack.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                               .Returns(actorSocketIdentity);

            var router = new MessageRouter(socketFactory.Object,
                                           messageHandlerStack.Object,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage {Partition = partition});
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                messageHandlerStack.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
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

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage());
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
            var message = Message.Create(new SimpleMessage());
            message.SetReceiverNode(new SocketIdentifier(externalNode.Node.SocketIdentity));

            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m =>
                                       m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi)), It.Is<byte[]>(id => Unsafe.Equals(id, externalNode.Node.SocketIdentity))))
                                .Returns(externalNode);
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi))))
                                .Returns(SocketIdentifier.Create());

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
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

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), DistributionPattern.Broadcast);
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

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), DistributionPattern.Broadcast);
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

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);
                Assert.IsFalse(peerConnection.Connected);
                var socket = messageRouterSocketFactory.GetScaleoutBackendSocket();
                Assert.IsFalse(socket.IsConnected());
                var message = Message.Create(new SimpleMessage());
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
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage());
                messageRouterSocketFactory.GetScaleoutFrontendSocket().DeliverMessage(message);

                var messageOut = messageRouterSocketFactory.GetScaleoutFrontendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(message, messageOut);
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfUnhandledMessageReceivedFromOtherNode_RouterUnregistersSelfAndRequestsDiscovery()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
                var message = Message.Create(new DiscoverMessageRouteMessage
                                             {
                                                 MessageContract = new MessageContract
                                                                   {
                                                                       Version = messageIdentifier.Version,
                                                                       Identity = messageIdentifier.Identity,
                                                                       Partition = messageIdentifier.Partition
                                                                   }
                                             });
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsFalse(internalRoutingTable.CanRouteMessage(messageIdentifier));
                clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>()), Times.Never());
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
            serviceMessageHandlers = new[] {new MessageRouteDiscoveryHandler(clusterMonitorProvider.Object, internalRoutingTable)};

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
                                             });
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsTrue(internalRoutingTable.CanRouteMessage(messageIdentifier));
                clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageIdentifier>>(ids => ids.First().Equals(messageIdentifier))), Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfRegisterExternalMessageRouteMessageReceived_AllRoutesAreAddedToExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            serviceMessageHandlers = new[] {new ExternalMessageRouteRegistrationHandler(externalRoutingTable, clusterMembership.Object, logger)};

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
                                             });
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
            var externalRoutingTable = new ExternalRoutingTable(logger);
            serviceMessageHandlers = new[] {new MessageRouteUnregistrationHandler(externalRoutingTable)};

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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

        [Test]
        public void UnregisterMessageRouteMessage_DeletesClusterMember()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            serviceMessageHandlers = new[] {new RouteUnregistrationHandler(externalRoutingTable, clusterMembership.Object)};

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var socket = messageRouterSocketFactory.GetRouterSocket();
                var message = new UnregisterNodeMessageRouteMessage
                              {
                                  Uri = "tcp://127.0.0.1:5000",
                                  SocketIdentity = SocketIdentifier.CreateIdentity()
                              };
                socket.DeliverMessage(Message.Create(message));
                Thread.Sleep(AsyncOp);

                clusterMembership.Verify(m => m.DeleteClusterMember(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == message.Uri
                                                                                               && Unsafe.Equals(e.Identity, message.SocketIdentity))),
                                         Times.Once());
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void IfAllMessageRoutesUnregisteredForNode_SocketIsDisconnected()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            serviceMessageHandlers = new[] {new MessageRouteUnregistrationHandler(externalRoutingTable)};

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
        public void IfUnregisterNodeMessageRouteMessageAndConnectionWasEstablished_SocketIsDisconnected()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            serviceMessageHandlers = new[] {new RouteUnregistrationHandler(externalRoutingTable, clusterMembership.Object)};

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
                var message = Message.Create(new UnregisterNodeMessageRouteMessage
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
            var externalRoutingTable = new ExternalRoutingTable(logger);
            serviceMessageHandlers = new[] {new RouteUnregistrationHandler(externalRoutingTable, clusterMembership.Object)};

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitorProvider.Object,
                                           serviceMessageHandlers,
                                           membershipConfiguration,
                                           logger);
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
                var message = Message.Create(new UnregisterNodeMessageRouteMessage
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

        private static IMessage SendMessageOverMessageHub()
        {
            var logger = new Mock<ILogger>();
            var sockrtFactory = new Mock<ISocketFactory>();
            var socket = new StubSocket();
            sockrtFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            var callback = CallbackPoint.Create<SimpleMessage>();

            var messageHub = new MessageHub(sockrtFactory.Object,
                                            new CallbackHandlerStack(),
                                            new MessageHubConfiguration(),
                                            logger.Object);
            messageHub.Start();
            messageHub.EnqueueRequest(message, callback);
            Thread.Sleep(AsyncOpCompletionDelay);

            return socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        }

        private static void StartMessageRouter(IMessageRouter messageRouter)
        {
            messageRouter.Start(StartTimeout);
            Thread.Sleep(AsyncOpCompletionDelay);
        }
    }
}