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
using rawf.Tests.Actor.Setup;

namespace rawf.Tests.Actor
{
    [TestFixture]
    public class ActorHostTests
    {
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);

        [Test]
        public void TestAssignActor_RegistersActorHandlers()
        {
            var actorHandlersMap = new ActorHandlersMap();

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          new ConnectivityProvider(),
                                          new HostConfiguration(string.Empty));
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
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

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
                                          new ConnectivityProvider(),
                                          new HostConfiguration(string.Empty));
            actorHost.Start();
        }

        [Test]
        public void TestSyncActorResponse_SendImmediately()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var actorHost = new ActorHost(actorHandlersMap,
                                          new MessagesCompletionQueue(),
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(TimeSpan.FromMilliseconds(100));

            var messageOut = socket.GetSentMessages().Last();

            CollectionAssert.AreEqual(messageOut.Body, messageIn.Body);
        }

        [Test]
        public void TestExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);

            var errorMessage = Guid.NewGuid().ToString();
            var actorHost = new ActorHost(actorHandlersMap, new MessagesCompletionQueue(), connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new ExceptionActor());
            actorHost.Start();

            var messageIn = Message.CreateFlowStartMessage(new SimpleMessage {Message = errorMessage}, SimpleMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(AsyncOpCompletionDelay);

            var messageOut = socket.GetSentMessages().Last();

            Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
        }

        [Test]
        public void TestAsyncActorResult_IsSentAfterCompletion()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);
            var messageCompletionQueue = new Mock<IMessagesCompletionQueue>();
            messageCompletionQueue.Setup(m => m.GetMessages(It.IsAny<CancellationToken>()))
                                  .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

            var actorHost = new ActorHost(actorHandlersMap,
                                          messageCompletionQueue.Object,
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new EchoActor());
            actorHost.Start();

            var delay = TimeSpan.FromMilliseconds(200);
            var asyncMessage = new AsyncMessage {Delay = delay};
            var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(delay);
            Thread.Sleep(AsyncOpCompletionDelay);

            messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncMessage(amc)),
                                                         It.IsAny<CancellationToken>()), Times.Once);
            messageCompletionQueue.Verify(m => m.GetMessages(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void TestAsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateDealerSocket()).Returns(socket);
            var messageCompletionQueue = new Mock<IMessagesCompletionQueue>();
            messageCompletionQueue.Setup(m => m.GetMessages(It.IsAny<CancellationToken>()))
                                  .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

            var actorHost = new ActorHost(actorHandlersMap,
                                          messageCompletionQueue.Object,
                                          connectivityProvider.Object,
                                          new HostConfiguration(string.Empty));
            actorHost.AssignActor(new ExceptionActor());
            actorHost.Start();

            var delay = TimeSpan.FromMilliseconds(200);
            var error = Guid.NewGuid().ToString();
            var asyncMessage = new AsyncExceptionMessage
                               {
                                   Delay = delay, 
                                   ErrorMessage = error
                               };
            var messageIn = Message.CreateFlowStartMessage(asyncMessage, AsyncExceptionMessage.MessageIdentity);
            socket.DeliverMessage(messageIn);

            Thread.Sleep(delay);
            Thread.Sleep(AsyncOpCompletionDelay);

            messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncExceptionMessage(amc)),
                                                         It.IsAny<CancellationToken>()), Times.Once);
            messageCompletionQueue.Verify(m => m.GetMessages(It.IsAny<CancellationToken>()), Times.Once);
        }

        private static bool IsAsyncMessage(AsyncMessageContext amc)
        {
            return Unsafe.Equals(amc.OutMessage.Identity, AsyncMessage.MessageIdentity);
        }

        private static bool IsAsyncExceptionMessage(AsyncMessageContext amc)
        {
            return Unsafe.Equals(amc.OutMessage.Identity, ExceptionMessage.MessageIdentity);
        }
    }
}