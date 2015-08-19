using System;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Actors;
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
    public class ActorHostTests
    {
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(500);
        //private readonly INodeConfiguration emptyNodeConfiguration;
        private readonly IClusterConfiguration emptyClusterConfiguration;
        private readonly RouterConfiguration routerConfiguration;
        private readonly string localhost = "tcp://localhost:43";

        public ActorHostTests()
        {
            //emptyNodeConfiguration = new NodeConfiguration(localhost, localhost);
            emptyClusterConfiguration = new ClusterConfiguration();
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateNew()),
                                      RouterAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateNew())
                                  };
        }

        [Test]
        public void TestAssignActor_RegistersActorHandlers()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var logger = new Mock<ILogger>();
            var actorRegistrationsQueue = new AsyncQueue<IActor>();

            var actorHost = new ActorHost(new SocketFactory(),
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          actorRegistrationsQueue,
                                          routerConfiguration,
                                          logger.Object);
            actorHost.AssignActor(new EchoActor());

            var registration = actorRegistrationsQueue.GetConsumingEnumerable(CancellationToken.None).First();
            Assert.IsTrue(registration.GetInterfaceDefinition().Any(id => id.Message.Identity == SimpleMessage.MessageIdentity));
            Assert.IsTrue(registration.GetInterfaceDefinition().Any(id => id.Message.Version == Message.CurrentVersion));
        }

        [Test]
        public void TestStartingActorHost_SendsActorRegistrationMessage()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var socketFactory = new Mock<ISocketFactory>();
            var logger = new Mock<ILogger>();
            var socket = new StubSocket();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger.Object);
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var registration = socket.GetSentMessages().First();
            var payload = new RegisterMessageHandlersMessage
                          {
                              SocketIdentity = socket.GetIdentity(),
                              MessageHandlers = actorHandlersMap
                                  .GetMessageHandlerIdentifiers()
                                  .Select(mh => new MessageHandlerRegistration
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version
                                                })
                                  .ToArray()
                          };
            var regMessage = Message.Create(payload, RegisterMessageHandlersMessage.MessageIdentity);

            CollectionAssert.AreEqual(registration.Body, regMessage.Body);
        }

        [Test]
        public void TestStartingActorHostWithoutActorAssigned_DoesntThrowException()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var socketFactory = new Mock<ISocketFactory>();
            var logger = new Mock<ILogger>();
            var socket = new StubSocket();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger.Object);
            actorHost.Start();

            logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
            logger.Verify(m => m.ErrorFormat(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Test]
        public void TestSyncActorResponse_SendImmediately()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var socketFactory = new Mock<ISocketFactory>();
            var logger = new Mock<ILogger>();
            var socket = new StubSocket();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger.Object);
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            var messageOut = socket.GetSentMessages().BlockingFirst();

            CollectionAssert.AreEqual(messageOut.Body, messageIn.Body);
            CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
        }

        [Test]
        public void TestExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var errorMessage = Guid.NewGuid().ToString();
            var actorHandlersMap = new ActorHandlerMap();
            var socketFactory = new Mock<ISocketFactory>();
            var logger = new Mock<ILogger>();
            var socket = new StubSocket();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger.Object);
            actorHost.AssignActor(new ExceptionActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new SimpleMessage {Message = errorMessage}, SimpleMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            var messageOut = socket.GetSentMessages().BlockingLast(AsyncOp);

            Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
            CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
        }

        //[Test]
        //public void TestAsyncActorResult_IsAddedToMessageCompletionQueue()
        //{
        //  var actorHandlersMap = new ActorHandlersMap();
        //  var connectivityProvider = new Mock<IConnectivityProvider>();
        //  var socket = new StubSocket();
        //  connectivityProvider.Setup(m => m.CreateRoutableSocket()).Returns(socket);
        //  connectivityProvider.Setup(m => m.CreateOneWaySocket()).Returns(new StubSocket());

        //  var messageCompletionQueue = new Mock<IMessagesCompletionQueue>();
        //  messageCompletionQueue.Setup(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()))
        //                        .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

        //  var actorHost = new ActorHost(actorHandlersMap,
        //                                messageCompletionQueue.Object,
        //                                connectivityProvider.Object);
        //  actorHost.AssignActor(new EchoActor());
        //  actorHost.Start();

        //  var delay = AsyncOp;
        //  var asyncMessage = new AsyncMessage {Delay = delay};
        //  var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
        //  socket.DeliverMessage(messageIn);

        //  Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

        //  messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncMessage(amc)),
        //                                               It.IsAny<CancellationToken>()),
        //                                Times.Once);
        //  messageCompletionQueue.Verify(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()), Times.Once);
        //}

        //[Test]
        //public void TestAsyncActorResult_IsSentAfterCompletion()
        //{
        //  var actorHandlersMap = new ActorHandlersMap();
        //  var connectivityProvider = new Mock<IConnectivityProvider>();
        //  var syncSocket = new StubSocket();
        //  var asyncSocket = new StubSocket();
        //  connectivityProvider.Setup(m => m.CreateRoutableSocket()).Returns(syncSocket);
        //  connectivityProvider.Setup(m => m.CreateOneWaySocket()).Returns(asyncSocket);

        //  var actorHost = new ActorHost(actorHandlersMap,
        //                                new MessagesCompletionQueue(),
        //                                connectivityProvider.Object);
        //  actorHost.AssignActor(new EchoActor());
        //  actorHost.Start();

        //  var delay = AsyncOp;
        //  var asyncMessage = new AsyncMessage {Delay = delay};
        //  var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
        //  syncSocket.DeliverMessage(messageIn);

        //  Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

        //  var sentMessages = asyncSocket.GetSentMessages();
        //  var messageOut = sentMessages.Last();

        //  Assert.AreEqual(1, sentMessages.Count());
        //  CollectionAssert.AreEqual(AsyncMessage.MessageIdentity, messageOut.Identity);
        //  Assert.AreEqual(delay, messageOut.GetPayload<AsyncMessage>().Delay);
        //  CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
        //}

        //[Test]
        //public void TestAsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        //{
        //  var actorHandlersMap = new ActorHandlersMap();
        //  var connectivityProvider = new Mock<IConnectivityProvider> {CallBase = true};
        //  var syncSocket = new StubSocket();
        //  var asyncSocket = new StubSocket();
        //  connectivityProvider.Setup(m => m.CreateRoutableSocket()).Returns(syncSocket);
        //  connectivityProvider.Setup(m => m.CreateOneWaySocket()).Returns(asyncSocket);
        //  var messageCompletionQueue = new MessagesCompletionQueue();

        //  var actorHost = new ActorHost(actorHandlersMap,
        //                                messageCompletionQueue,
        //                                connectivityProvider.Object);
        //  actorHost.AssignActor(new ExceptionActor());
        //  actorHost.Start();

        //  var delay = AsyncOp;
        //  var error = Guid.NewGuid().ToString();
        //  var asyncMessage = new AsyncExceptionMessage
        //                     {
        //                       Delay = delay,
        //                       ErrorMessage = error
        //                     };
        //  var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncExceptionMessage.MessageIdentity);
        //  syncSocket.DeliverMessage(messageIn);

        //  Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

        //  var sentMessages = asyncSocket.GetSentMessages();
        //  var messageOut = asyncSocket.GetSentMessages().Last();

        //  Assert.AreEqual(1, sentMessages.Count());
        //  CollectionAssert.AreEqual(ExceptionMessage.MessageIdentity, messageOut.Identity);
        //}

        //[Test]
        //[Ignore]
        //public void TestSyncMessageResponseHasSets_CorrelationId_CallbackIdentity_CallbackReceiverIdentity()
        //{
        //    //TODO: Implement
        //}

        private static bool IsAsyncMessage(AsyncMessageContext amc)
        {
            return Unsafe.Equals(amc.OutMessage.Identity, AsyncMessage.MessageIdentity);
        }
    }
}