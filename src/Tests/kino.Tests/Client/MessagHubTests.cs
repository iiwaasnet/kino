using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Client;
using kino.Core.Connectivity;
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

namespace kino.Tests.Client
{
    [TestFixture]
    public class MessagHubTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(2);
        private MessageHubSocketFactory messageHubSocketFactory;
        private readonly string localhost = "tcp://localhost:43";
        private Mock<ISocketFactory> socketFactory;
        private MessageHubConfiguration config;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private ILogger logger;
        private Mock<ICallbackHandlerStack> callbackHandlerStack;
        private Mock<ISecurityProvider> securityProvider;

        [SetUp]
        public void Setup()
        {
            callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            logger = new Mock<ILogger>().Object;
            messageHubSocketFactory = new MessageHubSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(messageHubSocketFactory.CreateSocket);
            config = new MessageHubConfiguration
                     {
                         RouterUri = new Uri(localhost)
                     };
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.SecurityDomainIsAllowed(It.IsAny<string>())).Returns(true);
        }

        [Test]
        public void OnMessageHubStart_RegisterationMessageIsSentAsGlobalRegistration()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var sendingSocket = messageHubSocketFactory.GetSendingSocket();
                var receivingSocket = messageHubSocketFactory.GetReceivingSocket();
                var message = sendingSocket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.IsNotNull(message);
                var registration = message.GetPayload<RegisterInternalMessageRouteMessage>();
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), registration.SocketIdentity);
                var handler = registration.GlobalMessageContracts.First();
                CollectionAssert.AreEqual(IdentityExtensions.Empty, handler.Version);
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), handler.Identity);
                Assert.IsNull(registration.LocalMessageContracts);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void EnqueueRequest_RegistersMessageAndExceptionHandlers()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                messageHub.EnqueueRequest(message, callback);

                Thread.Sleep(AsyncOp);

                callbackHandlerStack.Verify(m => m.Push(It.Is<CorrelationId>(c => Unsafe.Equals(c.Value, message.CorrelationId)),
                                                        It.IsAny<IPromise>(),
                                                        It.Is<IEnumerable<MessageIdentifier>>(en => ContainsMessageAndExceptionRegistrations(en))),
                                            Times.Once);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void EnqueueRequest_SendsMessageWithCallbackReceiverIdentityEqualsToReceivingSocketIdentity()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                messageHub.EnqueueRequest(message, callback);

                Thread.Sleep(AsyncOp);

                var messageOut = (Message) messageHubSocketFactory.GetSendingSocket().GetSentMessages().Last();
                var receivingSocket = messageHubSocketFactory.GetReceivingSocket();

                Assert.IsNotNull(messageOut);
                CollectionAssert.AreEqual(receivingSocket.GetIdentity(), messageOut.CallbackReceiverIdentity);
                CollectionAssert.AreEqual(callback.MessageIdentifiers, messageOut.CallbackPoint);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void WhenMessageReceived_CorrespondingPromiseResultSet()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

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
        public void WhenPromiseIsDisposed_ItsCallbackIsRemoved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                Thread.Sleep(AsyncOpCompletionDelay);

                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                Assert.IsNull(callbackHandlerStack.Pop(new CallbackHandlerKey
                                                       {
                                                           Version = callback.MessageIdentifiers.Single().Version,
                                                           Identity = callback.MessageIdentifiers.Single().Identity,
                                                           Partition = callback.MessageIdentifiers.Single().Partition,
                                                           Correlation = promise.CorrelationId.Value
                                                       }));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void WhenPromiseResultIsSet_ItsCallbackIsRemoved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                Thread.Sleep(AsyncOpCompletionDelay);

                promise.Dispose();

                Assert.IsNull(callbackHandlerStack.Pop(new CallbackHandlerKey
                                                       {
                                                           Version = callback.MessageIdentifiers.Single().Version,
                                                           Identity = callback.MessageIdentifiers.Single().Identity,
                                                           Partition = callback.MessageIdentifiers.Single().Partition,
                                                           Correlation = promise.CorrelationId.Value
                                                       }));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void WhenExceptionMessageReceived_PromiseThrowsException()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                var errorMessage = Guid.NewGuid().ToString();
                var exception = Message.Create(new ExceptionMessage {Exception = new Exception(errorMessage)});
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(exception);

                Assert.Throws<AggregateException>(() =>
                                                  {
                                                      var response = promise.GetResponse().Result;
                                                  },
                                                  errorMessage);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void WhenMessageReceivedAndNoHandlerRegistered_PromiseIsNotResolved()
        {
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack.Object,
                                            config,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

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

        private bool ContainsMessageAndExceptionRegistrations(IEnumerable<MessageIdentifier> registrations)
        {
            return registrations.Any(h => Unsafe.Equals(h.Identity, MessageIdentifier.Create<SimpleMessage>().Identity))
                   && registrations.Any(h => Unsafe.Equals(h.Version, KinoMessages.Exception.Version))
                   && registrations.Any(h => Unsafe.Equals(h.Partition, KinoMessages.Exception.Partition));
        }
    }
}