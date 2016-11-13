using System;
using System.Collections.Generic;
using System.Linq;
using kino.Client;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;
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
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private ILogger logger;
        private Mock<ICallbackHandlerStack> callbackHandlerStack;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<IRouterConfigurationProvider> routerConfigurationProvider;

        [SetUp]
        public void Setup()
        {
            callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            logger = new Mock<ILogger>().Object;
            messageHubSocketFactory = new MessageHubSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(messageHubSocketFactory.CreateSocket);
            var routerConfiguration = new RouterConfiguration
                                      {
                                          RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateIdentity())
                                      };
            routerConfigurationProvider = new Mock<IRouterConfigurationProvider>();
            routerConfigurationProvider.Setup(m => m.GetRouterConfiguration()).Returns(routerConfiguration);
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            CreateMessageHub();
        }

        [Test]
        public void WhenMessageHubStart_RegistrationMessageIsSentAsGlobalRegistration()
        {
            var messageHub = CreateMessageHub();
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
            var messageHub = CreateMessageHub();
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                messageHub.EnqueueRequest(message, callback);

                AsyncOp.Sleep();

                callbackHandlerStack.Verify(m => m.Push(It.Is<CorrelationId>(c => Equals(c.Value, message.CorrelationId)),
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
            var messageHub = CreateMessageHub();
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                messageHub.EnqueueRequest(message, callback);

                AsyncOp.Sleep();

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
            var messageHub = CreateMessageHub();
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
        public void WhenResultMessageIsDelivered_PromiseIsDisposedAndItsCallbackIsRemoved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(socketFactory.Object,
                                            callbackHandlerStack,
                                            routerConfigurationProvider.Object,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                //
                var promise = messageHub.EnqueueRequest(message, callback);

                AsyncOpCompletionDelay.Sleep();

                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                AsyncOpCompletionDelay.Sleep();
                //
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
                                            routerConfigurationProvider.Object,
                                            securityProvider.Object,
                                            performanceCounterManager.Object,
                                            logger);
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                AsyncOpCompletionDelay.Sleep();

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
            var messageHub = CreateMessageHub();
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
            var messageHub = CreateMessageHub();
            try
            {
                messageHub.Start();

                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();

                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns((IPromise) null);
                messageHubSocketFactory.GetReceivingSocket().DeliverMessage(message);

                AsyncOpCompletionDelay.Sleep();

                Assert.IsFalse(promise.GetResponse().Wait(AsyncOpCompletionDelay));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        private IMessageHub CreateMessageHub()
            => new MessageHub(socketFactory.Object,
                              callbackHandlerStack.Object,
                              routerConfigurationProvider.Object,
                              securityProvider.Object,
                              performanceCounterManager.Object,
                              logger);

        private static bool ContainsMessageAndExceptionRegistrations(IEnumerable<MessageIdentifier> registrations)
            => registrations.Any(h => Equals(h.Identity, MessageIdentifier.Create<SimpleMessage>().Identity))
               && registrations.Any(h => Equals(h.Version, KinoMessages.Exception.Version))
               && registrations.Any(h => Equals(h.Partition, KinoMessages.Exception.Partition));
    }
}