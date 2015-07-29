using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Frontend;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Frontend
{
    [TestFixture]
    public class MessagHubTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);

        [Test]
        public void TestOnMessageHubStart_RegisterationMessageIsSent()
        {
            var sendingSocket = new StubSocket();
            var receivingSocket = new StubSocket();
            receivingSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(sendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(receivingSocket);

            var messageHub = new MessageHub(connectivityProvider.Object, new CallbackHandlerStack());
            messageHub.Start();

            var message = sendingSocket.GetSentMessages().Last();

            Assert.IsNotNull(message);
            var registration = message.GetPayload<RegisterMessageHandlersMessage>();
            CollectionAssert.AreEqual(receivingSocket.GetIdentity(), registration.SocketIdentity);
            var handler = registration.Registrations.First();
            CollectionAssert.AreEqual(Message.CurrentVersion, handler.Version);
            CollectionAssert.AreEqual(receivingSocket.GetIdentity(), handler.Identity);
            Assert.AreEqual(IdentityType.Callback, handler.IdentityType);
        }

        [Test]
        public void TestEnqueueRequest_RegistersMessageAndExceptionHandlers()
        {
            var sendingSocket = new StubSocket();
            var receivingSocket = new StubSocket();
            receivingSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(sendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(receivingSocket);
            var callbackHandlerStack = new Mock<ICallbackHandlerStack>();

            var messageHub = new MessageHub(connectivityProvider.Object, callbackHandlerStack.Object);
            messageHub.Start();

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            messageHub.EnqueueRequest(message, callback);

            Thread.Sleep(AsyncOp);

            callbackHandlerStack.Verify(m => m.Push(It.Is<CorrelationId>(c => Unsafe.Equals(c.Value, message.CorrelationId)),
                                                   It.IsAny<IPromise>(),
                                                   It.Is<IEnumerable<MessageHandlerIdentifier>>(en => ContainsMessageAndExceptionRegistrations(en))),
                                                   Times.Once);


        }

        [Test]
        public void TestEnqueueRequest_SendsMessageWithCallbackReceiverIdentityEqualsToReceivingSocketIdentity()
        {
            var sendingSocket = new StubSocket();
            var receivingSocket = new StubSocket();
            receivingSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(sendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(receivingSocket);
            var callbackHandlerStack = new Mock<ICallbackHandlerStack>();

            var messageHub = new MessageHub(connectivityProvider.Object, callbackHandlerStack.Object);
            messageHub.Start();

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            messageHub.EnqueueRequest(message, callback);

            Thread.Sleep(AsyncOp);

            var messageOut = sendingSocket.GetSentMessages().Last();

            Assert.IsNotNull(messageOut);
            CollectionAssert.AreEqual(receivingSocket.GetIdentity(), messageOut.CallbackReceiverIdentity);
            CollectionAssert.AreEqual(callback.MessageIdentity, messageOut.CallbackIdentity);

        }

        [Test]
        public void TestWhenMessageReceived_CorrespondingPromiseResultSet()
        {
            var sendingSocket = new StubSocket();
            var receivingSocket = new StubSocket();
            receivingSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(sendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(receivingSocket);
            var callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            

            var messageHub = new MessageHub(connectivityProvider.Object, callbackHandlerStack.Object);
            messageHub.Start();

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            var promise = messageHub.EnqueueRequest(message, callback);
            callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
            receivingSocket.DeliverMessage(message);

            var response = promise.GetResponse().Result;

            Assert.IsNotNull(response);
            Assert.AreEqual(message, response);
        }

        [Test]
        public void TestWhenExceptionMessageReceived_PromiseThrowsException()
        {
            var sendingSocket = new StubSocket();
            var receivingSocket = new StubSocket();
            receivingSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(sendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(receivingSocket);
            var callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            

            var messageHub = new MessageHub(connectivityProvider.Object, callbackHandlerStack.Object);
            messageHub.Start();

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            var promise = messageHub.EnqueueRequest(message, callback);
            callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
            var errorMessage = Guid.NewGuid().ToString();
            var exception = Message.Create(new ExceptionMessage {Exception = new Exception(errorMessage)}, ExceptionMessage.MessageIdentity);
            receivingSocket.DeliverMessage(exception);

            Assert.Throws<AggregateException>(() => { var response = promise.GetResponse().Result; }, errorMessage);
            Assert.DoesNotThrow(() => { var response = promise.GetResponse(); });
        }

        [Test]
        public void TestWhenMessageReceivedAndNoHandlerRegistered_PromiseIsNotResolved()
        {
            var sendingSocket = new StubSocket();
            var receivingSocket = new StubSocket();
            receivingSocket.SetIdentity(Guid.NewGuid().ToString().GetBytes());
            var connectivityProvider = new Mock<IConnectivityProvider>();
            connectivityProvider.Setup(m => m.CreateMessageHubSendingSocket()).Returns(sendingSocket);
            connectivityProvider.Setup(m => m.CreateMessageHubReceivingSocket()).Returns(receivingSocket);
            var callbackHandlerStack = new Mock<ICallbackHandlerStack>();


            var messageHub = new MessageHub(connectivityProvider.Object, callbackHandlerStack.Object);
            messageHub.Start();

            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

            var promise = messageHub.EnqueueRequest(message, callback);
            callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns((IPromise)null);
            receivingSocket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            Assert.IsFalse(promise.GetResponse().Wait(AsyncOp));
        }

        private bool ContainsMessageAndExceptionRegistrations(IEnumerable<MessageHandlerIdentifier> registrations)
        {
            return registrations.Any(h => Unsafe.Equals(h.Identity, SimpleMessage.MessageIdentity))
                   && registrations.Any(h => Unsafe.Equals(h.Identity, ExceptionMessage.MessageIdentity));
        }
    }
}