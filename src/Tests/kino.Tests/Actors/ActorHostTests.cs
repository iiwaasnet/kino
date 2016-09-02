using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Actors;
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

namespace kino.Tests.Actors
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
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private Mock<ISecurityProvider> securityProvider;

        [SetUp]
        public void Setup()
        {
            loggerMock = new Mock<ILogger>();
            logger = new Mock<ILogger>().Object;
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            actorHandlersMap = new ActorHandlerMap();
            actorHostSocketFactory = new ActorHostSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(actorHostSocketFactory.CreateSocket);
            routerConfiguration = new RouterConfiguration
                                  {
                                      RouterAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateIdentity())
                                  };
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
        }

        [Test]
        public void AssignActor_RegistersActorHandlers()
        {
            var actorRegistrationsQueue = new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>();

            var actorHost = new ActorHost(new SocketFactory(null),
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          actorRegistrationsQueue,
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            var partition = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
            actorHost.AssignActor(new ConfigurableActor(new[]
                                                        {
                                                            new MessageHandlerDefinition
                                                            {
                                                                Handler = _ => null,
                                                                Message = new MessageDefinition(messageIdentifier.Identity,
                                                                                                messageIdentifier.Version,
                                                                                                partition)
                                                            }
                                                        }));

            var registrations = actorRegistrationsQueue.GetConsumingEnumerable(CancellationToken.None).First();

            Assert.IsTrue(registrations.Any(id => id.Identifier.Identity == messageIdentifier.Identity));
            Assert.IsTrue(registrations.Any(id => id.Identifier.Version == messageIdentifier.Version));
            Assert.IsTrue(registrations.Any(id => id.Identifier.Partition == messageIdentifier.Partition));
        }

        [Test]
        public void StartingActorHost_SendsActorRegistrationMessageForBothGlobalAndLocalRegistrations()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            var partition = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<AsyncMessage>(partition);
            var actorWithGlobalAndLocalHandlers = new ConfigurableActor(new[]
                                                                        {
                                                                            new MessageHandlerDefinition
                                                                            {
                                                                                Handler = _ => null,
                                                                                Message = new MessageDefinition(messageIdentifier.Identity,
                                                                                                                messageIdentifier.Version,
                                                                                                                partition)
                                                                            },
                                                                            new MessageHandlerDefinition
                                                                            {
                                                                                Handler = _ => null,
                                                                                Message = new MessageDefinition(messageIdentifier.Identity,
                                                                                                                messageIdentifier.Version,
                                                                                                                IdentityExtensions.Empty),
                                                                                KeepRegistrationLocal = true
                                                                            }
                                                                        });
            actorHost.AssignActor(actorWithGlobalAndLocalHandlers);
            try
            {
                StartActorHost(actorHost);

                var routableSocket = actorHostSocketFactory.GetRoutableSocket();
                var registration = actorHostSocketFactory.GetRegistrationSocket()
                                                         .GetSentMessages()
                                                         .BlockingLast(AsyncOpCompletionDelay);

                Assert.IsNotNull(registration);
                var payload = new RegisterInternalMessageRouteMessage
                              {
                                  SocketIdentity = routableSocket.GetIdentity(),
                                  LocalMessageContracts = actorWithGlobalAndLocalHandlers.GetInterfaceDefinition()
                                                                                         .Where(mh => mh.KeepRegistrationLocal)
                                                                                         .Select(mh => new MessageContract
                                                                                                       {
                                                                                                           Identity = mh.Message.Identity,
                                                                                                           Version = mh.Message.Version,
                                                                                                           Partition = mh.Message.Partition
                                                                                                       })
                                                                                         .ToArray(),
                                  GlobalMessageContracts = actorWithGlobalAndLocalHandlers.GetInterfaceDefinition()
                                                                                          .Where(mh => !mh.KeepRegistrationLocal)
                                                                                          .Select(mh => new MessageContract
                                                                                                        {
                                                                                                            Identity = mh.Message.Identity,
                                                                                                            Version = mh.Message.Version,
                                                                                                            Partition = mh.Message.Partition
                                                                                                        })
                                                                                          .ToArray()
                              };
                var regMessage = registration.GetPayload<RegisterInternalMessageRouteMessage>();

                CollectionAssert.AreEqual(payload.Identity, regMessage.Identity);
                CollectionAssert.AreEqual(payload.Version, regMessage.Version);
                CollectionAssert.AreEqual(payload.Partition, regMessage.Partition);
                CollectionAssert.AreEqual(payload.SocketIdentity, regMessage.SocketIdentity);
                Assert.AreEqual(payload.GlobalMessageContracts.Length, regMessage.GlobalMessageContracts.Length);
                Assert.AreEqual(payload.LocalMessageContracts.Length, regMessage.LocalMessageContracts.Length);
                Assert.AreEqual(payload.GlobalMessageContracts.Length,
                                payload.GlobalMessageContracts.Select(mc => new MessageIdentifier(mc.Version, mc.Identity, mc.Partition))
                                       .Intersect(regMessage.GlobalMessageContracts
                                                            .Select(mc => new MessageIdentifier(mc.Version, mc.Identity, mc.Partition))).Count());
                Assert.AreEqual(payload.LocalMessageContracts.Length,
                                payload.LocalMessageContracts.Select(mc => new MessageIdentifier(mc.Version, mc.Identity, mc.Partition))
                                       .Intersect(regMessage.LocalMessageContracts
                                                            .Select(mc => new MessageIdentifier(mc.Version, mc.Identity, mc.Partition))).Count());
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void StartingActorHostWithoutActorAssigned_DoesntThrowException()
        {
            var logger = loggerMock;
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger.Object);
            try
            {
                StartActorHost(actorHost);

                logger.Verify(m => m.Error(It.IsAny<object>()), Times.Never);
                logger.Verify(m => m.Error(It.IsAny<string>()), Times.Never);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void SyncActorResponse_SendImmediately()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage());
                var socket = actorHostSocketFactory.GetRoutableSocket();
                socket.DeliverMessage(messageIn);

                var messageOut = socket.GetSentMessages().BlockingFirst(AsyncOpCompletionDelay);

                CollectionAssert.AreEqual(messageIn.Identity, messageOut.Identity);
                CollectionAssert.AreEqual(messageIn.Partition, messageOut.Partition);
                CollectionAssert.AreEqual(messageIn.Version, messageOut.Version);
                CollectionAssert.AreEqual(messageIn.Body, messageOut.Body);
                CollectionAssert.AreEqual(messageIn.CorrelationId, messageOut.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void ExceptionThrownFromActorHandler_DeliveredAsExceptionMessage()
        {
            var errorMessage = Guid.NewGuid().ToString();

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new ExceptionActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage {Content = errorMessage});
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
        public void AsyncActorResult_IsSentAfterCompletion()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var delay = AsyncOp;
                var asyncMessage = new AsyncMessage {Delay = delay};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                var messageOut = actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                var messageIdentifier = MessageIdentifier.Create<AsyncMessage>();
                Assert.IsTrue(messageOut.Equals(messageIdentifier));
                Assert.AreEqual(delay, messageOut.GetPayload<AsyncMessage>().Delay);
                CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void AsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
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
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                var messageOut = actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.IsTrue(messageOut.Equals(KinoMessages.Exception));
                CollectionAssert.AreEqual(messageOut.CorrelationId, messageIn.CorrelationId);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void AsyncActorResult_IsAddedToMessageCompletionQueue()
        {
            var messageCompletionQueue = new Mock<IAsyncQueue<AsyncMessageContext>>();
            messageCompletionQueue.Setup(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()))
                                  .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          messageCompletionQueue.Object,
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var delay = AsyncOp;
                var asyncMessage = new AsyncMessage {Delay = delay};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
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

        [Test]
        public void CallbackReceiverIdentities_AreCopiedFromIncomingMessageProcessedSync()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                messageIn.RegisterCallbackPoint(callbackReceiver, MessageIdentifier.Create<SimpleMessage>());

                var socket = actorHostSocketFactory.GetRoutableSocket();
                socket.DeliverMessage(messageIn);

                var messageOut = (Message) socket.GetSentMessages().BlockingFirst(AsyncOpCompletionDelay);

                CollectionAssert.AreEqual(messageIn.CallbackPoint, messageOut.CallbackPoint);
                CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void CallbackReceiverIdentities_AreCopiedFromIncomingMessageProcessedAsync()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new EchoActor());
            try
            {
                StartActorHost(actorHost);

                var delay = AsyncOp;
                var asyncMessage = new AsyncMessage {Delay = delay};
                var messageIn = (Message) Message.CreateFlowStartMessage(asyncMessage);
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                messageIn.RegisterCallbackPoint(callbackReceiver, MessageIdentifier.Create<SimpleMessage>());

                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                var messageOut = (Message) actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                CollectionAssert.AreEqual(messageIn.CallbackPoint, messageOut.CallbackPoint);
                CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void IfCallbackIsRegistered_SyncExceptionMessageIsDeliveredToCallbackReceiver()
        {
            var errorMessage = Guid.NewGuid().ToString();

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new ExceptionActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = errorMessage});
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                var callbackPoints = new[] {MessageIdentifier.Create<SimpleMessage>(), KinoMessages.Exception};
                messageIn.RegisterCallbackPoint(callbackReceiver, callbackPoints);

                var socket = actorHostSocketFactory.GetRoutableSocket();
                socket.DeliverMessage(messageIn);

                var messageOut = (Message) socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
                Assert.IsTrue(messageOut.Equals(KinoMessages.Exception));
                CollectionAssert.Contains(messageOut.CallbackPoint, KinoMessages.Exception);
                CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void IfCallbackIsRegistered_AsyncExceptionMessageIsDeliveredToCallbackReceiver()
        {
            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
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
                var messageIn = (Message) Message.CreateFlowStartMessage(asyncMessage);
                var callbackReceiver = Guid.NewGuid().ToByteArray();
                var callbackPoints = new[] {MessageIdentifier.Create<SimpleMessage>(), KinoMessages.Exception};
                messageIn.RegisterCallbackPoint(callbackReceiver, callbackPoints);

                actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

                Thread.Sleep(AsyncOpCompletionDelay + AsyncOp);

                var messageOut = (Message) actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(error, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
                Assert.IsTrue(messageOut.Equals(KinoMessages.Exception));
                CollectionAssert.Contains(messageOut.CallbackPoint, KinoMessages.Exception);
                CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void ExceptionMessage_HasDomainSet()
        {
            var kinoDomain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.GetDomain(KinoMessages.Exception.Identity)).Returns(kinoDomain);

            var actorHost = new ActorHost(socketFactory.Object,
                                          actorHandlersMap,
                                          new AsyncQueue<AsyncMessageContext>(),
                                          new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                          routerConfiguration,
                                          securityProvider.Object,
                                          performanceCounterManager.Object,
                                          logger);
            actorHost.AssignActor(new ExceptionActor());
            try
            {
                StartActorHost(actorHost);

                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage());
                var socket = actorHostSocketFactory.GetRoutableSocket();
                socket.DeliverMessage(messageIn);

                var messageOut = socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

                Assert.AreEqual(kinoDomain, messageOut.Domain);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        private static bool IsAsyncMessage(AsyncMessageContext amc)
            => amc.OutMessages.First().Equals(MessageIdentifier.Create<AsyncMessage>());

        private static void StartActorHost(IActorHost actorHost)
        {
            actorHost.Start();
            Thread.Sleep(AsyncOpCompletionDelay);
        }
    }
}