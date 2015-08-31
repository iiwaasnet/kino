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
using kino.Tests.Backend.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;
using IMessageTracer = kino.Client.IMessageTracer;

namespace kino.Tests.Frontend
{
    [TestFixture]
    public class MessagHubTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(2);
        private MessageHubSocketFactory messageHubSocketFactory;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ISocketFactory> socketFactory;
        private Mock<ILogger> loggerMock;
        private IMessageHubConfiguration config;
        private ILogger logger;
        private Mock<ICallbackHandlerStack> callbackHandlerStack;
        private Mock<IMessageTracer> messageTracer;

        [SetUp]
        public void Setup()
        {
            callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            loggerMock = new Mock<ILogger>();
            logger = new Logger("default");
            messageHubSocketFactory = new MessageHubSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(messageHubSocketFactory.CreateSocket);
            config = new MessageHubConfiguration
                     {
                         RouterUri = new Uri(localhost)
                     };
            messageTracer = new Mock<IMessageTracer>();
        }

        [Test]
        public void TestOnMessageHubStart_RegisterationMessageIsSent()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            messageTracer.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var sendingSocket = messageHubSocketFactory.GetSendingSocket();
                var receivingSocket = messageHubSocketFactory.GetReceivingSocket();
                var message = sendingSocket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.IsNotNull(message);
                var registration = message.GetPayload<RegisterMessageHandlersMessage>();
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), registration.SocketIdentity);
                var handler = registration.MessageHandlers.First();
                CollectionAssert.AreEqual(Message.CurrentVersion, handler.Version);
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), handler.Identity);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestEnqueueRequest_RegistersMessageAndExceptionHandlers()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            messageTracer.Object,
                                            logger);
            try
            {
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
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestEnqueueRequest_SendsMessageWithCallbackReceiverIdentityEqualsToReceivingSocketIdentity()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            messageTracer.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
                var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

                messageHub.EnqueueRequest(message, callback);

                Thread.Sleep(AsyncOp);

                var messageOut = messageHubSocketFactory.GetSendingSocket().GetSentMessages().Last();
                var receivingSocket = messageHubSocketFactory.GetReceivingSocket();

                Assert.IsNotNull(messageOut);
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), messageOut.CallbackReceiverIdentity);
                CollectionAssert.AreEqual(callback.MessageIdentity, messageOut.CallbackIdentity);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenMessageReceived_CorrespondingPromiseResultSet()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            messageTracer.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
                var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                var response = promise.GetResponse().Result;

                Assert.IsNotNull(response);
                Assert.AreEqual(message, response);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenExceptionMessageReceived_PromiseThrowsException()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            messageTracer.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
                var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                var errorMessage = Guid.NewGuid().ToString();
                var exception = Message.Create(new ExceptionMessage {Exception = new Exception(errorMessage)}, ExceptionMessage.MessageIdentity);
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(exception);

                Assert.Throws<AggregateException>(() => { var response = promise.GetResponse().Result; }, errorMessage);
                Assert.DoesNotThrow(() => { var response = promise.GetResponse(); });
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void TestWhenMessageReceivedAndNoHandlerRegistered_PromiseIsNotResolved()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            messageTracer.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
                var callback = new CallbackPoint(SimpleMessage.MessageIdentity);

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns((IPromise) null);
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                Thread.Sleep(AsyncOpCompletionDelay);

                Assert.IsFalse(promise.GetResponse().Wait(AsyncOpCompletionDelay));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        private bool ContainsMessageAndExceptionRegistrations(IEnumerable<MessageHandlerIdentifier> registrations)
        {
            return registrations.Any(h => Unsafe.Equals(h.Identity, SimpleMessage.MessageIdentity))
                   && registrations.Any(h => Unsafe.Equals(h.Identity, ExceptionMessage.MessageIdentity));
        }
    }
}