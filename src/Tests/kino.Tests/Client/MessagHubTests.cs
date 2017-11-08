using System;
using System.Collections.Generic;
using System.Linq;
using kino.Client;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using Xunit;

namespace kino.Tests.Client
{
    public class MessagHubTests
    {
        private static readonly TimeSpan ReceiveMessageDelay = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan ReceiveMessageCompletionDelay = ReceiveMessageDelay + TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);
        private readonly MessageHubSocketFactory messageHubSocketFactory;
        private readonly string localhost = "tcp://localhost:43";
        private readonly Mock<ISocketFactory> socketFactory;
        private readonly ILogger logger;
        private readonly Mock<ICallbackHandlerStack> callbackHandlerStack;
        private readonly Mock<ISecurityProvider> securityProvider;
        private MessageHub messageHub;
        private readonly Mock<ILocalSocket<IMessage>> routerSocket;
        private readonly Mock<ILocalSendingSocket<InternalRouteRegistration>> registrationSocket;
        private readonly SocketEndpoint scaleOutAddress;
        private readonly Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private readonly Mock<ILocalSocketFactory> localSocketFactory;
        private readonly Mock<ILocalSocket<IMessage>> receivingSocket;

        public MessagHubTests()
        {
            callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            logger = new Mock<ILogger>().Object;
            messageHubSocketFactory = new MessageHubSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(messageHubSocketFactory.CreateSocket);
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            routerSocket = new Mock<ILocalSocket<IMessage>>();
            registrationSocket = new Mock<ILocalSendingSocket<InternalRouteRegistration>>();
            localSocketFactory = new Mock<ILocalSocketFactory>();
            receivingSocket = new Mock<ILocalSocket<IMessage>>();
            localSocketFactory.Setup(m => m.Create<IMessage>()).Returns(receivingSocket.Object);
            scaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            messageHub = CreateMessageHub();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WhenMessageHubStarts_RegistrationMessageIsSentAsWithProperGlobalVisibility(bool keepRegistrationLocal)
        {
            try
            {
                messageHub = CreateMessageHub(keepRegistrationLocal);
                //
                messageHub.Start();
                //
                Func<InternalRouteRegistration, bool> globalRegistration = msg => msg.KeepRegistrationLocal == keepRegistrationLocal
                                                                                  && msg.DestinationSocket == receivingSocket.Object;
                registrationSocket.Verify(m => m.Send(It.Is<InternalRouteRegistration>(msg => globalRegistration(msg))));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Fact]
        public void EnqueueRequest_RegistersMessageAndExceptionHandlers()
        {
            try
            {
                messageHub.Start();
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                //
                messageHub.EnqueueRequest(message, callback);
                AsyncOpCompletionDelay.Sleep();
                //
                callbackHandlerStack.Verify(m => m.Push(It.IsAny<IPromise>(),
                                                        It.Is<IEnumerable<MessageIdentifier>>(en => ContainsMessageAndExceptionRegistrations(en))),
                                            Times.Once);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Fact]
        public void EnqueueRequest_SendsMessageWithCallbackSetToThisMessageHub()
        {
            try
            {
                messageHub.Start();
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                //
                messageHub.EnqueueRequest(message, callback);
                AsyncOpCompletionDelay.Sleep();
                //
                routerSocket.WaitUntilMessageSent(RouterSocketIsReceiver);
            }
            finally
            {
                messageHub.Stop();
            }

            bool RouterSocketIsReceiver(IMessage msg)
                => Unsafe.ArraysEqual(msg.As<Message>().ReceiverNodeIdentity, scaleOutAddress.Identity)
                   && Unsafe.ArraysEqual(msg.As<Message>().ReceiverIdentity, messageHub.ReceiverIdentifier.Identity);
        }

        [Fact]
        public void WhenMessageReceived_CorrespondingPromiseResultSet()
        {
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                var promise = messageHub.EnqueueRequest(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                //
                receivingSocket.SetupMessageReceived(message);
                messageHub.Start();
                var response = promise.GetResponse().Result;
                //
                Assert.Equal(message, response);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Fact]
        public void WhenResultMessageIsDelivered_PromiseIsDisposedAndItsCallbackIsRemoved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(callbackHandlerStack,
                                            routerSocket.Object,
                                            registrationSocket.Object,
                                            localSocketFactory.Object,
                                            scaleOutConfigurationProvider.Object,
                                            securityProvider.Object,
                                            logger);
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<NullMessage>();
                var promise = messageHub.EnqueueRequest(message, callback);
                var callbackMessage = Message.Create(new NullMessage()).As<Message>();
                callbackMessage.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                                      Guid.NewGuid().ToByteArray(),
                                                      callback.MessageIdentifiers,
                                                      promise.CallbackKey.Value);
                receivingSocket.SetupMessageReceived(callbackMessage, ReceiveMessageDelay);
                //
                messageHub.Start();
                ReceiveMessageCompletionDelay.Sleep();
                //
                Assert.Null(callbackHandlerStack.Pop(new CallbackHandlerKey
                                                     {
                                                         Version = callback.MessageIdentifiers.Single().Version,
                                                         Identity = callback.MessageIdentifiers.Single().Identity,
                                                         Partition = callback.MessageIdentifiers.Single().Partition,
                                                         CallbackKey = promise.CallbackKey.Value
                                                     }));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Fact]
        public void WhenExceptionMessageReceived_PromiseThrowsException()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(callbackHandlerStack,
                                            routerSocket.Object,
                                            registrationSocket.Object,
                                            localSocketFactory.Object,
                                            scaleOutConfigurationProvider.Object,
                                            securityProvider.Object,
                                            logger);
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<NullMessage>();
                var promise = messageHub.EnqueueRequest(message, callback);
                var errorMessage = Guid.NewGuid().ToString();
                var exception = Message.Create(new ExceptionMessage {Exception = new Exception(errorMessage)}).As<Message>();
                exception.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                                Guid.NewGuid().ToByteArray(),
                                                callback.MessageIdentifiers,
                                                promise.CallbackKey.Value);
                receivingSocket.SetupMessageReceived(exception, ReceiveMessageDelay);
                //
                messageHub.Start();
                ReceiveMessageCompletionDelay.Sleep();
                //
                var err = Record.Exception(() =>
                                           {
                                               var _ = promise.GetResponse().Result;
                                           });
                Assert.Equal(errorMessage, err.InnerException.Message);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Fact]
        public void WhenMessageReceivedAndNoHandlerRegistered_PromiseIsNotResolved()
        {
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(callbackHandlerStack,
                                            routerSocket.Object,
                                            registrationSocket.Object,
                                            localSocketFactory.Object,
                                            scaleOutConfigurationProvider.Object,
                                            securityProvider.Object,
                                            logger);
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<NullMessage>();
                var promise = messageHub.EnqueueRequest(message, callback);
                var callbackMessage = Message.Create(new NullMessage()).As<Message>();
                var nonExistingCallbackKey = -1L;
                callbackMessage.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                                      Guid.NewGuid().ToByteArray(),
                                                      callback.MessageIdentifiers,
                                                      nonExistingCallbackKey);
                receivingSocket.SetupMessageReceived(callbackMessage, ReceiveMessageDelay);
                //
                messageHub.Start();
                ReceiveMessageCompletionDelay.Sleep();
                //
                Assert.False(promise.GetResponse().Wait(AsyncOpCompletionDelay));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        private MessageHub CreateMessageHub(bool keepRegistrationLocal = false)
            => new MessageHub(callbackHandlerStack.Object,
                              routerSocket.Object,
                              registrationSocket.Object,
                              localSocketFactory.Object,
                              scaleOutConfigurationProvider.Object,
                              securityProvider.Object,
                              logger,
                              keepRegistrationLocal);

        private static bool ContainsMessageAndExceptionRegistrations(IEnumerable<MessageIdentifier> registrations)
            => registrations.Any(h => Equals(h.Identity, MessageIdentifier.Create<SimpleMessage>().Identity))
               && registrations.Any(h => Equals(h.Version, KinoMessages.Exception.Version))
               && registrations.Any(h => Equals(h.Partition, KinoMessages.Exception.Partition));
    }
}