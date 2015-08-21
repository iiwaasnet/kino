using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Client;
using rawf.Connectivity;
using rawf.Diagnostics;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;
using rawf.Tests.Backend.Setup;
using rawf.Tests.Helpers;

namespace rawf.Tests.Backend
{
    [TestFixture]
    public class MessageRouterTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(3);
        private RouterConfiguration routerConfiguration;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ILogger> logger;
        private Mock<ISocketFactory> socketFactory;
        private StubSocket routerSocket;
        private Mock<IClusterMonitor> clusterMonitor;

        [SetUp]
        public void Setup()
        {
            clusterMonitor = new Mock<IClusterMonitor>();
            socketFactory = new Mock<ISocketFactory>();
            routerSocket = new StubSocket();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
            logger = new Mock<ILogger>();
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateNew()),
                                      RouterAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateNew())
                                  };
        }

        [Test]
        public void TestMessageRouterUponStart_CreatesRouterLocalAndTwoScaleoutSockets()
        {
            var router = new MessageRouter(socketFactory.Object,
                                           new InternalRoutingTable(),
                                           new ExternalRoutingTable(logger.Object),
                                           new ClusterConfiguration(),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           logger.Object);
            try
            {
                router.Start();

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
                                           new ExternalRoutingTable(logger.Object),
                                           new ClusterConfiguration(),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           logger.Object);
            try
            {
                router.Start();

                var messageIdentity = Guid.NewGuid().ToByteArray();
                var version = Guid.NewGuid().ToByteArray();
                var socketIdentity = Guid.NewGuid().ToByteArray();
                var message = Message.Create(new RegisterMessageHandlersMessage
                                             {
                                                 SocketIdentity = socketIdentity,
                                                 MessageHandlers = new[]
                                                                   {
                                                                       new MessageHandlerRegistration
                                                                       {
                                                                           Identity = messageIdentity,
                                                                           Version = version
                                                                       }
                                                                   }
                                             },
                                             RegisterMessageHandlersMessage.MessageIdentity);
                routerSocket.DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                var identifier = internalRoutingTable.Pop(new MessageHandlerIdentifier(version, messageIdentity));

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
                                           new ExternalRoutingTable(logger.Object),
                                           new ClusterConfiguration(),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           logger.Object);
            try
            {
                router.Start();

                var message = (Message) SendMessageOverMessageHub();

                var callbackSocketIdentity = message.CallbackReceiverIdentity;
                var callbackIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, callbackSocketIdentity);
                internalRoutingTable.Setup(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(callbackIdentifier))))
                                    .Returns(new SocketIdentifier(callbackSocketIdentity));

                routerSocket.DeliverMessage(message);

                Thread.Sleep(AsyncOp);

                internalRoutingTable.Verify(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(callbackIdentifier))), Times.Once());
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
            var actorIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var messageHandlerStack = new Mock<IInternalRoutingTable>();
            messageHandlerStack.Setup(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(actorIdentifier))))
                               .Returns(actorSocketIdentity);

            var router = new MessageRouter(socketFactory.Object,
                                           messageHandlerStack.Object,
                                           new ExternalRoutingTable(logger.Object),
                                           new ClusterConfiguration(),
                                           routerConfiguration,
                                           clusterMonitor.Object,
                                           logger.Object);
            router.Start();

            var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
            routerSocket.DeliverMessage(message);

            Thread.Sleep(AsyncOpCompletionDelay);

            messageHandlerStack.Verify(m => m.Pop(It.Is<MessageHandlerIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
        }

        //[Test]
        //public void TestIfLocalRoutingTableHasNoMessageHandlerRegistration_MessageRoutedToOtherNodes()
        //{
        //    var connectivityProvider = new Mock<IConnectivityProvider>();
        //    var routerSocket = new StubSocket();
        //    var scaleOutBackEndSocket = new StubSocket();
        //    scaleOutBackEndSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
        //    connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
        //    connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(scaleOutBackEndSocket);
        //    connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(new StubSocket());

        //    var messageHandlerStack = new Mock<IInternalRoutingTable>();

        //    var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack.Object);
        //    router.Start();

        //    var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
        //    routerSocket.DeliverMessage(message);

        //    Thread.Sleep(AsyncOp);

        //    var messageOut = scaleOutBackEndSocket.GetSentMessages().Last();

        //    Assert.AreEqual(message, messageOut);
        //}

        //[Test]
        //public void TestMessageReceivedFromOtherNode_ForwardedToLocalRouterSocket()
        //{
        //    var connectivityProvider = new Mock<IConnectivityProvider>();
        //    var routerSocket = new StubSocket();
        //    var scaleOutFrontEndSocket = new StubSocket();
        //    scaleOutFrontEndSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
        //    connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(routerSocket);
        //    connectivityProvider.Setup(m => m.CreateScaleOutBackendSocket()).Returns(new StubSocket());
        //    connectivityProvider.Setup(m => m.CreateScaleOutFrontendSocket()).Returns(scaleOutFrontEndSocket);

        //    var messageHandlerStack = new Mock<IInternalRoutingTable>();

        //    var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack.Object);
        //    router.Start();

        //    var message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
        //    scaleOutFrontEndSocket.DeliverMessage(message);

        //    Thread.Sleep(AsyncOp);

        //    var messageOut = scaleOutFrontEndSocket.GetSentMessages().Last();

        //    Assert.AreEqual(message, messageOut);
        //}

        private static IMessage SendMessageOverMessageHub()
        {
            var logger = new Mock<ILogger>();
            var sockrtFactory = new Mock<ISocketFactory>();
            var socket = new StubSocket();
            sockrtFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            var messageHub = new MessageHub(sockrtFactory.Object,
                                            new CallbackHandlerStack(new ExpirableItemCollection<CorrelationId>(logger.Object)),
                                            new MessageHubConfiguration(),
                                            logger.Object);
            messageHub.Start();
            messageHub.EnqueueRequest(message, callback);
            Thread.Sleep(AsyncOpCompletionDelay);

            return socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        }
    }
}