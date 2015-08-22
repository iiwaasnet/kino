using System;
using System.Collections.Concurrent;
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
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private RouterConfiguration routerConfiguration;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ILogger> loggerMock;
        private ILogger logger;
        private ActorHandlerMap actorHandlersMap;
        private Mock<ISocketFactory> socketFactory;
        private ActorHostSocketFactory actorHostSocketFactory;

        [SetUp]
        public void Setup()
        {
            loggerMock = new Mock<ILogger>();
            logger = new Logger("default");
            actorHandlersMap = new ActorHandlerMap();
            actorHostSocketFactory = new ActorHostSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(actorHostSocketFactory.CreateSocket);
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateNew()),
                                      RouterAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateNew())
                                  };
        }

        [Test]
        public void TestAssignActor_RegistersActorHandlers()
        {
            var actorRegistrationsQueue = new AsyncQueue<IActor>();

            var actorHost = new ActorHost(new SocketFactory(),
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          actorRegistrationsQueue,
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new EchoActor());

            var registration = actorRegistrationsQueue.GetConsumingEnumerable(CancellationToken.None).First();
            Assert.IsTrue(registration.GetInterfaceDefinition().Any(id => id.Message.Identity == SimpleMessage.MessageIdentity));
            Assert.IsTrue(registration.GetInterfaceDefinition().Any(id => id.Message.Version == Message.CurrentVersion));
        }

        [Test]
        public void TestStartingActorHost_SendsActorRegistrationMessage()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var routableSocket = actorHostSocketFactory.GetRoutableSocket();
                var registration = actorHostSocketFactory
                    .GetRegistrationSocket()
                    .GetSentMessages()
                    .BlockingLast(AsyncOpCompletionDelay);

                Assert.IsNotNull(registration);
                var payload = new RegisterMessageHandlersMessage
                              {
                                  SocketIdentity = routableSocket.GetIdentity(),
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
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void TestStartingActorHostWithoutActorAssigned_DoesntThrowException()
        {
            var logger = loggerMock;
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger.Object);
            try
            {
                StartActorHost(actorHost);

                logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
                logger.Verify(m => m.ErrorFormat(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void TestSyncActorResponse_SendImmediately()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
                var socket = actorHostSocketFactory.GetRoutableSocket();
                socket.DeliverMessage(messageIn);

                var messageOut = socket.GetSentMessages().BlockingFirst(AsyncOpCompletionDelay);

                CollectionAssert.AreEqual(messageIn.Identity.GetString(), messageOut.Identity.GetString());
                CollectionAssert.AreEqual(messageIn.Body, messageOut.Body);
                CollectionAssert.AreEqual(messageIn.CorrelationId, messageOut.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void TestExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var errorMessage = Guid.NewGuid().ToString();

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new ExceptionActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage {Message = errorMessage}, SimpleMessage.MessageIdentity);
                var socket = actorHostSocketFactory.GetRoutableSocket();
                socket.DeliverMessage(messageIn);

                var messageOut = socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
                CollectionAssert.AreEqual(messageIn.CorrelationId, messageOut.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void TestAsyncActorResult_IsSentAfterCompletion()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var delay = AsyncOp;
                var asyncMessage = new AsyncMessage {Delay = delay};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                var messageOut = actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                CollectionAssert.AreEqual(AsyncMessage.MessageIdentity, messageOut.Identity);
                Assert.AreEqual(delay, messageOut.GetPayload<AsyncMessage>().Delay);
                CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void TestAsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new ExceptionActor());
            try
            {
                StartActorHost(actorHost);

                var error = Guid.NewGuid().ToString();
                var asyncMessage = new AsyncExceptionMessage
                                   {
                                       Delay = AsyncOp,
                                       ErrorMessage = error
                                   };
                var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncExceptionMessage.MessageIdentity);
                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                var messageOut = actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                CollectionAssert.AreEqual(ExceptionMessage.MessageIdentity.GetString(), messageOut.Identity.GetString());
                CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void TestAsyncActorResult_IsAddedToMessageCompletionQueue()
        {
            var messageCompletionQueue = new Mock<IAsyncQueue<AsyncMessageContext>>();
            messageCompletionQueue.Setup(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()))
                                  .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          messageCompletionQueue.Object,
                                          new AsyncQueue<IActor>(),
                                          routerConfiguration,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var delay = AsyncOp;
                var asyncMessage = new AsyncMessage {Delay = delay};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncMessage(amc)),
                                                             It.IsAny<CancellationToken>()),
                                              Times.Once);
                messageCompletionQueue.Verify(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        private static bool IsAsyncMessage(AsyncMessageContext amc)
        {
            return Unsafe.Equals(amc.OutMessage.Identity, AsyncMessage.MessageIdentity);
        }

        private static void StartActorHost(IActorHost actorHost)
        {
            actorHost.Start();
            Thread.Sleep(AsyncOpCompletionDelay);
        }
    }
}