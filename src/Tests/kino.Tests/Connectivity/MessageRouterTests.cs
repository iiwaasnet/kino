using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Client;
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
using IMessageTracer = kino.Connectivity.IMessageTracer;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class MessageRouterTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(4);
        private RouterConfiguration routerConfiguration;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ILogger> loggerMock;
        private ILogger logger;
        private Mock<ISocketFactory> socketFactory;
        private Mock<IClusterMonitor> clusterMonitor;
        private MessageRouterSocketFactory messageRouterSocketFactory;
        private Mock<IMessageTracer> messageTracer;
        private ClusterMembershipConfiguration membershipConfiguration;

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
            messageRouterSocketFactory = new MessageRouterSocketFactory(routerConfiguration);
            clusterMonitor = new Mock<IClusterMonitor>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(messageRouterSocketFactory.CreateSocket);
            loggerMock = new Mock<ILogger>();
            messageTracer = new Mock<IMessageTracer>();
            logger = new Logger("default");
        }

        [Test]
        public void TestMessageRouterUponStart_CreatesRouterLocalAndTwoScaleoutSockets()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
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
        public void TestRegisterMessageHandlers_AddsActorIdentifier()
        {
            var internalRoutingTable = new InternalRoutingTable();

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
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
                                                 MessageContracts = new[]
                                                                    {
                                                                        new MessageContract
                                                                        {
                                                                            Identity = messageIdentity,
                                                                            Version = version
                                                                        }
                                                                    }
                                             },
                                             RegisterInternalMessageRouteMessage.MessageIdentity);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity));

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
        public void TestHandlerForReceiverIdentifier_HasHighestPriority()
        {
            var internalRoutingTable = new Mock<IInternalRoutingTable>();

            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable.Object,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = (Message) SendMessageOverMessageHub();

                var callbackSocketIdentity = message.CallbackReceiverIdentity;
                var callbackIdentifier = new MessageIdentifier(Message.CurrentVersion, callbackSocketIdentity);
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
        public void TestMessageIsRouted_BasedOnHandlerIdentities()
        {
            var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
            var actorIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var messageHandlerStack = new Mock<IInternalRoutingTable>();
            messageHandlerStack.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                               .Returns(actorSocketIdentity);

            var router = new MessageRouter(socketFactory.Object,
                                           messageHandlerStack.Object,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
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
        public void TestIfLocalRoutingTableHasNoMessageHandlerRegistration_MessageRoutedToOtherNodes()
        {
            var externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>()))
                                .Returns(new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity()));

            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable.Object,
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
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
        public void TestMessageReceivedFromOtherNode_ForwardedToLocalRouterSocket()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
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
        public void TestIfUnhandledMessageReceivedFromOtherNode_RouterUnregistersSelfAndRequestsDiscovery()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
                var message = (Message) Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
                message.PushRouterAddress(new SocketEndpoint(new Uri("tcp://127.1.1.1:9000"), SocketIdentifier.CreateIdentity()));
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
        public void TestIfUnhandledMessageReceivedFromLocalActor_RouterRequestsDiscovery()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
                var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
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
        public void TestIfMessageRouterCannotHandleMessage_SelfRegisterIsNotCalled()
        {
            var internalRoutingTable = new InternalRoutingTable();
            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
                var message = Message.Create(new DiscoverMessageRouteMessage
                                             {
                                                 MessageContract = new MessageContract
                                                                   {
                                                                       Version = messageIdentifier.Version,
                                                                       Identity = messageIdentifier.Identity
                                                                   }
                                             },
                                             DiscoverMessageRouteMessage.MessageIdentity);
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
        public void TestIfMessageRouterCanHandleMessage_SelfRegisterIsCalled()
        {
            var internalRoutingTable = new InternalRoutingTable();
            var router = new MessageRouter(socketFactory.Object,
                                           internalRoutingTable,
                                           new ExternalRoutingTable(logger),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
                internalRoutingTable.AddMessageRoute(messageIdentifier, SocketIdentifier.Create());
                var message = Message.Create(new DiscoverMessageRouteMessage
                                             {
                                                 MessageContract = new MessageContract
                                                                   {
                                                                       Version = messageIdentifier.Version,
                                                                       Identity = messageIdentifier.Identity
                                                                   }
                                             },
                                             DiscoverMessageRouteMessage.MessageIdentity);
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
        public void TestIfRegisterExternalMessageRouteMessageReceived_AllRoutesAreAddedToExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity),
                                             new MessageIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity)
                                         };
                var socketIdentity = SocketIdentifier.CreateIdentity();
                var message = Message.Create(new RegisterExternalMessageRouteMessage
                                             {
                                                 Uri = "tcp://127.0.0.1:8000",
                                                 SocketIdentity = socketIdentity,
                                                 MessageContracts = messageIdentifiers.Select(mi => new MessageContract
                                                                                                    {
                                                                                                        Version = mi.Version,
                                                                                                        Identity = mi.Identity
                                                                                                    }).ToArray()
                                             },
                                             RegisterExternalMessageRouteMessage.MessageIdentity);
                messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                Assert.IsTrue(Unsafe.Equals(socketIdentity, externalRoutingTable.FindRoute(messageIdentifiers.First()).SocketIdentity));
                Assert.IsTrue(Unsafe.Equals(socketIdentity, externalRoutingTable.FindRoute(messageIdentifiers.Second()).SocketIdentity));
            }
            finally
            {
                router.Stop();
            }
        }

        [Test]
        public void TestIfUnregisterMessageRouteMessage_RoutesAreRemovedFromExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity),
                                             new MessageIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity)
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
                                                                                                        Identity = mi.Identity
                                                                                                    }).ToArray()
                                             },
                                             UnregisterMessageRouteMessage.MessageIdentity);

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
        public void TestIfUnregisterNodeMessageRouteMessage_AllRoutesAreRemovedFromExternalRoutingTable()
        {
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           externalRoutingTable,
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           messageTracer.Object,
                                           logger);
            try
            {
                StartMessageRouter(router);

                var messageIdentifiers = new[]
                                         {
                                             new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity),
                                             new MessageIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity)
                                         };

                var socketIdentity = SocketIdentifier.Create();
                var uri = new Uri("tcp://127.0.0.1:8000");
                messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
                var message = Message.Create(new UnregisterNodeMessageRouteMessage
                {
                                                 Uri = uri.ToSocketAddress(),
                                                 SocketIdentity = socketIdentity.Identity
                                             },
                                             UnregisterNodeMessageRouteMessage.MessageIdentity);

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
            var messageTracer = new Mock<kino.Client.IMessageTracer>();
            var logger = new Mock<ILogger>();
            var sockrtFactory = new Mock<ISocketFactory>();
            var socket = new StubSocket();
            sockrtFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            var messageHub = new MessageHub(sockrtFactory.Object,
                                            new CallbackHandlerStack(new ExpirableItemCollection<CorrelationId>(logger.Object)),
                                            new MessageHubConfiguration(),
                                            messageTracer.Object,
                                            logger.Object);
            messageHub.Start();
            messageHub.EnqueueRequest(message, callback);
            Thread.Sleep(AsyncOpCompletionDelay);

            return socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        }

        private static void StartMessageRouter(IMessageRouter messageRouter)
        {
            messageRouter.Start();
            Thread.Sleep(AsyncOpCompletionDelay);
        }
    }
}