using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Actors;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Sockets;
using rawf.Tests.Actor.Setup;

namespace rawf.Tests.Actor
{
    [TestFixture]
    public class ActorHostTests
    {
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private readonly IConnectivityConfiguration emptyConfiguration;

        public ActorHostTests()
        {
            emptyConfiguration = new ConnectivityConfiguration(string.Empty, string.Empty, string.Empty);
        }

        [Test]
        public void TestAssignActor_RegistersActorHandlers()
        {
            var actorHandlersMap = new ActorHandlersMap();

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          new ConnectivityProvider(new SocketProvider(),
                                                                   emptyConfiguration));
            actorHost.AssignActor(new EchoActor());

            var registration = actorHandlersMap.GetRegisteredIdentifiers().First();
            CollectionAssert.AreEqual(SimpleMessage.MessageIdentity, registration.Identity);
            CollectionAssert.AreEqual(Message.CurrentVersion, registration.Version);
        }

        [Test]
        public void TestStartingActorHost_SendsActorRegistrationMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateActorSyncSocket()).Returns(socket);
            connectivityProvider.Setup(m => m.CreateActorAsyncSocket()).Returns(new StubSocket());

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object);
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            Thread.Sleep(AsyncOp);

            var registration = socket.GetSentMessages().First();
            var payload = new RegisterMessageHandlers
                          {
                              SocketIdentity = socket.GetIdentity(),
                              Registrations = actorHandlersMap
                                  .GetRegisteredIdentifiers()
                                  .Select(mh => new MessageHandlerRegistration
                                                {
                                                    Identity = mh.Identity,
                                                    Version = mh.Version,
                                                    IdentityType = IdentityType.Actor
                                                })
                                  .ToArray()
                          };
            var regMessage = Message.Create(payload, RegisterMessageHandlers.MessageIdentity);

            CollectionAssert.AreEqual(registration.Body, regMessage.Body);
        }

        [Test]
        [ExpectedException]
        public void TestStartingActorHostWithoutActorAssigned_ThrowsException()
        {
            var actorHandlersMap = new ActorHandlersMap();

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          new ConnectivityProvider(new SocketProvider(), emptyConfiguration));
            actorHost.Start();
        }

        [Test]
        public void TestSyncActorResponse_SendImmediately()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateActorSyncSocket()).Returns(socket);
            connectivityProvider.Setup(m => m.CreateActorAsyncSocket()).Returns(new StubSocket());

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object);
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(AsyncOpCompletionDelay);

            var messageOut = socket.GetSentMessages().Last();

            CollectionAssert.AreEqual(messageOut.Body, messageIn.Body);
            CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
        }

        [Test]
        public void TestExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateActorSyncSocket()).Returns(socket);
            connectivityProvider.Setup(m => m.CreateActorAsyncSocket()).Returns(new StubSocket());

            var errorMessage = Guid.NewGuid().ToString();
            var actorHost = new ActorHost(actorHandlersMap, new MessagesCompletionQueue(), connectivityProvider.Object);
            actorHost.AssignActor(new ExceptionActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new SimpleMessage { Message = errorMessage }, SimpleMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(AsyncOpCompletionDelay);

            var messageOut = socket.GetSentMessages().Last();

            Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
            CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
        }

        [Test]
        public void TestAsyncActorResult_IsAddedToMessageCompletionQueue()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateActorSyncSocket()).Returns(socket);
            connectivityProvider.Setup(m => m.CreateActorAsyncSocket()).Returns(new StubSocket());

            var messageCompletionQueue = new Mock<IMessagesCompletionQueue>();
            messageCompletionQueue.Setup(m => m.GetMessages(It.IsAny<CancellationToken>()))
                                  .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

            var actorHost = new ActorHost(actorHandlersMap,
                                          messageCompletionQueue.Object,
                                          connectivityProvider.Object);
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var delay = AsyncOp;
            var asyncMessage = new AsyncMessage { Delay = delay };
            var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

            messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncMessage(amc)),
                                                         It.IsAny<CancellationToken>()), Times.Once);
            messageCompletionQueue.Verify(m => m.GetMessages(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void TestAsyncActorResult_IsSentAfterCompletion()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var syncSocket = new StubSocket();
            var asyncSocket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateActorSyncSocket()).Returns(syncSocket);
            connectivityProvider.Setup(m => m.CreateActorAsyncSocket()).Returns(asyncSocket);

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object);
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var delay = AsyncOp;
            var asyncMessage = new AsyncMessage { Delay = delay };
            var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
            syncSocket.DeliverMessage(messageIn);

            Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

            var sentMessages = asyncSocket.GetSentMessages();
            var messageOut = sentMessages.Last();

            Assert.AreEqual(1, sentMessages.Count());
            CollectionAssert.AreEqual(AsyncMessage.MessageIdentity, messageOut.Identity);
            Assert.AreEqual(delay, messageOut.GetPayload<AsyncMessage>().Delay);
            CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
        }

        [Test]
        public void TestAsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider> { CallBase = true };
            var syncSocket = new StubSocket();
            var asyncSocket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateActorSyncSocket()).Returns(syncSocket);
            connectivityProvider.Setup(m => m.CreateActorAsyncSocket()).Returns(asyncSocket);
            var messageCompletionQueue = new MessagesCompletionQueue();

            var actorHost = new ActorHost(actorHandlersMap,
                                          messageCompletionQueue,
                                          connectivityProvider.Object);
            actorHost.AssignActor(new ExceptionActor());
            actorHost.Start();

            var delay = AsyncOp;
            var error = Guid.NewGuid().ToString();
            var asyncMessage = new AsyncExceptionMessage
            {
                Delay = delay,
                ErrorMessage = error
            };
            var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncExceptionMessage.MessageIdentity);
            syncSocket.DeliverMessage(messageIn);

            Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

            var sentMessages = asyncSocket.GetSentMessages();
            var messageOut = asyncSocket.GetSentMessages().Last();

            Assert.AreEqual(1, sentMessages.Count());
            CollectionAssert.AreEqual(ExceptionMessage.MessageIdentity, messageOut.Identity);
        }

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