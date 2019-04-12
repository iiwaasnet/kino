using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using NUnit.Framework;

namespace kino.Tests.Client
{
    public class MessagHubTests
    {
        private static readonly TimeSpan ReceiveMessageDelay = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan ReceiveMessageCompletionDelay = ReceiveMessageDelay + TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);
        private MessageHubSocketFactory messageHubSocketFactory;
        private Mock<ISocketFactory> socketFactory;
        private Mock<ILogger> logger;
        private Mock<ICallbackHandlerStack> callbackHandlerStack;
        private Mock<ISecurityProvider> securityProvider;
        private MessageHub messageHub;
        private Mock<ILocalSocket<IMessage>> routerSocket;
        private Mock<ILocalSocket<InternalRouteRegistration>> registrationSocket;
        private SocketEndpoint scaleOutAddress;
        private Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private Mock<ILocalSocketFactory> localSocketFactory;
        private Mock<ILocalSocket<IMessage>> receivingSocket;

        [SetUp]
        public void Setup()
        {
            callbackHandlerStack = new Mock<ICallbackHandlerStack>();
            logger = new Mock<ILogger>();
            messageHubSocketFactory = new MessageHubSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(messageHubSocketFactory.CreateSocket);
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            routerSocket = new Mock<ILocalSocket<IMessage>>();
            registrationSocket = new Mock<ILocalSocket<InternalRouteRegistration>>();
            localSocketFactory = new Mock<ILocalSocketFactory>();
            receivingSocket = new Mock<ILocalSocket<IMessage>>();
            receivingSocket.Setup(m => m.CanReceive()).Returns(new ManualResetEvent(false));
            localSocketFactory.Setup(m => m.Create<IMessage>()).Returns(receivingSocket.Object);
            scaleOutAddress = SocketEndpoint.Parse("tcp://127.0.0.1:5000", Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            localSocketFactory.Setup(m => m.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket))
                              .Returns(routerSocket.Object);
            localSocketFactory.Setup(m => m.CreateNamed<InternalRouteRegistration>(NamedSockets.InternalRegistrationSocket))
                              .Returns(registrationSocket.Object);

            messageHub = CreateMessageHub();
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void WhenMessageHubStarts_RegistrationMessageIsSentAsWithProperGlobalVisibility(bool keepRegistrationLocal)
        {
            try
            {
                // arrange
                messageHub = CreateMessageHub(keepRegistrationLocal);
                receivingSocket.As<ILocalSendingSocket<IMessage>>().Setup(m => m.Equals(receivingSocket.Object)).Returns(true);
                // act
                messageHub.Start();
                // assert
                Func<InternalRouteRegistration, bool> globalRegistration = msg => msg.KeepRegistrationLocal == keepRegistrationLocal
                                                                                  && msg.DestinationSocket.Equals(receivingSocket.Object);
                registrationSocket.Verify(m => m.Send(It.Is<InternalRouteRegistration>(msg => globalRegistration(msg))));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void Send_RegistersMessageAndExceptionHandlers()
        {
            try
            {
                // arrange
                messageHub.Start();
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                // act
                messageHub.Send(message, callback);
                AsyncOpCompletionDelay.Sleep();
                // assert
                Func<IEnumerable<MessageIdentifier>, bool> hasAllRegistrations = registrations =>
                                                                                 {
                                                                                     return registrations.Any(h => Equals(h.Identity, MessageIdentifier.Create<SimpleMessage>().Identity))
                                                                                            && registrations.Any(h => Equals(h.Version, KinoMessages.Exception.Version))
                                                                                            && registrations.Any(h => Equals(h.Partition, KinoMessages.Exception.Partition));
                                                                                 };
                callbackHandlerStack.Verify(m => m.Push(It.IsAny<IPromise>(),
                                                        It.Is<IEnumerable<MessageIdentifier>>(en => hasAllRegistrations(en))),
                                            Times.Once);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void NoExceptionIsThrownAndMessageIsSent_IfExceptionCallbackIsRegisteredExplicitly()
        {
            try
            {
                // arrange
                messageHub.Start();
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage, ExceptionMessage>();
                // act
                messageHub.Send(message, callback);
                AsyncOpCompletionDelay.Sleep();
                // assert
                Func<IEnumerable<MessageIdentifier>, bool> hasAllRegistrations = registrations =>
                                                                                 {
                                                                                     return registrations.Any(h => Equals(h.Identity, MessageIdentifier.Create<SimpleMessage>().Identity))
                                                                                            && registrations.Any(h => Equals(h.Version, KinoMessages.Exception.Version))
                                                                                            && registrations.Any(h => Equals(h.Partition, KinoMessages.Exception.Partition));
                                                                                 };
                callbackHandlerStack.Verify(m => m.Push(It.IsAny<IPromise>(),
                                                        It.Is<IEnumerable<MessageIdentifier>>(en => hasAllRegistrations(en))),
                                            Times.Once);
                routerSocket.Verify(m => m.Send(It.IsAny<IMessage>()), Times.Once);
                logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void Send_SendsMessageWithCallbackSetToThisMessageHub()
        {
            // arrange
            try
            {
                messageHub.Start();
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                // act
                messageHub.Send(message, callback);
                AsyncOpCompletionDelay.Sleep();
                // assert
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

        [Test]
        public void WhenMessageReceived_CorrespondingPromiseResultSet()
        {
            // arrange
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<SimpleMessage>();
                var promise = messageHub.Send(message, callback);
                callbackHandlerStack.Setup(m => m.Pop(It.IsAny<CallbackHandlerKey>())).Returns(promise);
                // act
                receivingSocket.SetupMessageReceived(message);
                messageHub.Start();
                var response = promise.GetResponse().Result;
                // assert
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
            // arrange
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(callbackHandlerStack,
                                            localSocketFactory.Object,
                                            scaleOutConfigurationProvider.Object,
                                            securityProvider.Object,
                                            logger.Object);
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<NullMessage>();
                var promise = messageHub.Send(message, callback);
                var callbackMessage = Message.Create(new NullMessage()).As<Message>();
                callbackMessage.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                                      Guid.NewGuid().ToByteArray(),
                                                      callback.MessageIdentifiers,
                                                      promise.CallbackKey.Value);
                receivingSocket.SetupMessageReceived(callbackMessage, ReceiveMessageDelay);
                // act
                messageHub.Start();
                ReceiveMessageCompletionDelay.Sleep();
                // assert
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

        [Test]
        public void WhenExceptionMessageReceived_PromiseThrowsException()
        {
            // arrange
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(callbackHandlerStack,
                                            localSocketFactory.Object,
                                            scaleOutConfigurationProvider.Object,
                                            securityProvider.Object,
                                            logger.Object);
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<NullMessage>();
                var promise = messageHub.Send(message, callback);
                var errorMessage = Guid.NewGuid().ToString();
                var exc = new Exception(errorMessage);
                var exception = Message.Create(new ExceptionMessage
                                               {
                                                   Message = exc.Message,
                                                   ExceptionType = exc.GetType().ToString(),
                                                   StackTrace = exc.StackTrace
                                               }).As<Message>();
                exception.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                                Guid.NewGuid().ToByteArray(),
                                                callback.MessageIdentifiers,
                                                promise.CallbackKey.Value);
                receivingSocket.SetupMessageReceived(exception, ReceiveMessageDelay);
                // act
                messageHub.Start();
                ReceiveMessageCompletionDelay.Sleep();
                // assert
                Assert.Throws<AggregateException>(() =>
                                                  {
                                                      var _ = promise.GetResponse().Result;
                                                  });
                try
                {
                    var _ = promise.GetResponse().Result;
                }
                catch (Exception err)
                {
                    Assert.AreEqual(errorMessage, err.InnerException.Message);
                }
            }
            finally
            {
                messageHub.Stop();
            }
        }

        [Test]
        public void WhenMessageReceivedAndNoHandlerRegistered_PromiseIsNotResolved()
        {
            // arrange
            var callbackHandlerStack = new CallbackHandlerStack();
            var messageHub = new MessageHub(callbackHandlerStack,
                                            localSocketFactory.Object,
                                            scaleOutConfigurationProvider.Object,
                                            securityProvider.Object,
                                            logger.Object);
            try
            {
                var message = Message.CreateFlowStartMessage(new SimpleMessage());
                var callback = CallbackPoint.Create<NullMessage>();
                var promise = messageHub.Send(message, callback);
                var callbackMessage = Message.Create(new NullMessage()).As<Message>();
                var nonExistingCallbackKey = -1L;
                callbackMessage.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                                      Guid.NewGuid().ToByteArray(),
                                                      callback.MessageIdentifiers,
                                                      nonExistingCallbackKey);
                receivingSocket.SetupMessageReceived(callbackMessage, ReceiveMessageDelay);
                // act
                messageHub.Start();
                ReceiveMessageCompletionDelay.Sleep();
                // assert
                Assert.False(promise.GetResponse().Wait(AsyncOpCompletionDelay));
            }
            finally
            {
                messageHub.Stop();
            }
        }

        private MessageHub CreateMessageHub(bool keepRegistrationLocal = false)
            => new MessageHub(callbackHandlerStack.Object,
                              localSocketFactory.Object,
                              scaleOutConfigurationProvider.Object,
                              securityProvider.Object,
                              logger.Object,
                              keepRegistrationLocal);
    }
}