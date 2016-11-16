using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using kino.Actors;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Security;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Actors
{
    [TestFixture]
    public class ActorHostTests
    {
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);
        private Mock<ILogger> logger;
        private ActorHandlerMap actorHandlersMap;
        private Mock<ISecurityProvider> securityProvider;
        private ActorHost actorHost;
        private Mock<ILocalSocket<IMessage>> localRouterSocket;
        private Mock<ILocalSendingSocket<InternalRouteRegistration>> internalRegistrationSender;
        private Mock<ILocalSocketFactory> localSocketFactory;
        private Mock<ILocalSocket<IMessage>> receivingSocket;
        private Mock<IAsyncQueue<AsyncMessageContext>> asyncQueue;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            actorHandlersMap = new ActorHandlerMap();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            localRouterSocket = new Mock<ILocalSocket<IMessage>>();
            internalRegistrationSender = new Mock<ILocalSendingSocket<InternalRouteRegistration>>();
            localSocketFactory = new Mock<ILocalSocketFactory>();
            receivingSocket = new Mock<ILocalSocket<IMessage>>();
            localSocketFactory.Setup(m => m.Create<IMessage>()).Returns(receivingSocket.Object);
            asyncQueue = new Mock<IAsyncQueue<AsyncMessageContext>>();
            actorHost = new ActorHost(actorHandlersMap,
                                      asyncQueue.Object,
                                      new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
                                      securityProvider.Object,
                                      localRouterSocket.Object,
                                      internalRegistrationSender.Object,
                                      localSocketFactory.Object,
                                      logger.Object);
        }

        [Test]
        public void AssignActor_SendsRegisterationMessage()
        {
            var partition = Guid.NewGuid().ToByteArray();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
            try
            {
                StartActorHost(actorHost);
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
                AsyncOp.Sleep();
                Func<InternalRouteRegistration, bool> registrationRequest = (reg) => reg.MessageContracts.Any(id => Unsafe.ArraysEqual(id.Identifier.Identity, messageIdentifier.Identity)
                                                                                                                    && Unsafe.ArraysEqual(id.Identifier.Partition, messageIdentifier.Partition)
                                                                                                                    && id.Identifier.Version == messageIdentifier.Version);
                internalRegistrationSender.Verify(m => m.Send(It.Is<InternalRouteRegistration>(reg => registrationRequest(reg))), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void SyncActorResponse_SendImmediately()
        {
            try
            {
                actorHost.AssignActor(new EchoActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage());
                SetupMessageSend(receivingSocket, messageIn);
                //
                StartActorHost(actorHost);

                WaitUntilResponseSent(localRouterSocket);
                //
                asyncQueue.Verify(m => m.Enqueue(It.IsAny<AsyncMessageContext>(), It.IsAny<CancellationToken>()), Times.Never);
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

            try
            {
                actorHost.AssignActor(new ExceptionActor());
                var messageIn = Message.CreateFlowStartMessage(new SimpleMessage {Content = errorMessage});
                SetupMessageSend(receivingSocket, messageIn);
                //
                StartActorHost(actorHost);
                //
                Func<IMessage, bool> isExceptionMessage = m => m.GetPayload<ExceptionMessage>().Exception.Message == errorMessage;
                WaitUntilResponseSent(localRouterSocket, isExceptionMessage);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void AsyncActorResult_IsEnqueuedForCompletion()
        {
            try
            {
                actorHost.AssignActor(new EchoActor());

                var asyncMessage = new AsyncMessage {Delay = AsyncOp};
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                SetupMessageSend(receivingSocket, messageIn);
                //
                StartActorHost(actorHost);
                (AsyncOpCompletionDelay + AsyncOp).Sleep();
                //
                asyncQueue.Verify(m => m.Enqueue(It.IsAny<AsyncMessageContext>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        [Test]
        public void AsyncActorException_IsSentAfterCompletionAsExceptionMessage()
        {
            try
            {
                actorHost.AssignActor(new ExceptionActor());
                var error = Guid.NewGuid().ToString();
                var asyncMessage = new AsyncExceptionMessage
                                   {
                                       Delay = AsyncOp,
                                       ErrorMessage = error
                                   };
                var messageIn = Message.CreateFlowStartMessage(asyncMessage);
                SetupMessageSend(receivingSocket, messageIn);
                //
                StartActorHost(actorHost);
                (AsyncOpCompletionDelay + AsyncOp).Sleep();
                //
                asyncQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>( c => c.OutMessages.First().Equals(KinoMessages.Exception)), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                actorHost.Stop();
            }
        }

        //[Test]
        //public void AsyncActorResult_IsAddedToMessageCompletionQueue()
        //{
        //    var messageCompletionQueue = new Mock<IAsyncQueue<AsyncMessageContext>>();
        //    messageCompletionQueue.Setup(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()))
        //                          .Returns(new BlockingCollection<AsyncMessageContext>().GetConsumingEnumerable());

        //    var actorHost = new ActorHost(socketFactory.Object,
        //                                  actorHandlersMap,
        //                                  messageCompletionQueue.Object,
        //                                  new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
        //                                  routerConfiguration,
        //                                  securityProvider.Object,
        //                                  performanceCounterManager.Object,
        //                                  logger);
        //    actorHost.AssignActor(new EchoActor());
        //    try
        //    {
        //        StartActorHost(actorHost);

        //        var delay = AsyncOp;
        //        var asyncMessage = new AsyncMessage {Delay = delay};
        //        var messageIn = Message.CreateFlowStartMessage(asyncMessage);
        //        actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

        //        (AsyncOpCompletionDelay + AsyncOp).Sleep();

        //        messageCompletionQueue.Verify(m => m.Enqueue(It.Is<AsyncMessageContext>(amc => IsAsyncMessage(amc)),
        //                                                     It.IsAny<CancellationToken>()),
        //                                      Times.Once);
        //        messageCompletionQueue.Verify(m => m.GetConsumingEnumerable(It.IsAny<CancellationToken>()), Times.Once);
        //    }
        //    finally
        //    {
        //        actorHost.Stop();
        //    }
        //}

        //[Test]
        //public void CallbackReceiverIdentities_AreCopiedFromIncomingMessageProcessedSync()
        //{
        //    var actorHost = new ActorHost(socketFactory.Object,
        //                                  actorHandlersMap,
        //                                  new AsyncQueue<AsyncMessageContext>(),
        //                                  new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
        //                                  routerConfiguration,
        //                                  securityProvider.Object,
        //                                  performanceCounterManager.Object,
        //                                  logger);
        //    actorHost.AssignActor(new EchoActor());
        //    try
        //    {
        //        StartActorHost(actorHost);

        //        var messageIn = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
        //        var callbackReceiver = Guid.NewGuid().ToByteArray();
        //        messageIn.RegisterCallbackPoint(callbackReceiver, MessageIdentifier.Create<SimpleMessage>());

        //        var socket = actorHostSocketFactory.GetRoutableSocket();
        //        socket.DeliverMessage(messageIn);

        //        var messageOut = (Message) socket.GetSentMessages().BlockingFirst(AsyncOpCompletionDelay);

        //        CollectionAssert.AreEqual(messageIn.CallbackPoint, messageOut.CallbackPoint);
        //        CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
        //    }
        //    finally
        //    {
        //        actorHost.Stop();
        //    }
        //}

        //[Test]
        //public void CallbackReceiverIdentities_AreCopiedFromIncomingMessageProcessedAsync()
        //{
        //    var actorHost = new ActorHost(socketFactory.Object,
        //                                  actorHandlersMap,
        //                                  new AsyncQueue<AsyncMessageContext>(),
        //                                  new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
        //                                  routerConfiguration,
        //                                  securityProvider.Object,
        //                                  performanceCounterManager.Object,
        //                                  logger);
        //    actorHost.AssignActor(new EchoActor());
        //    try
        //    {
        //        StartActorHost(actorHost);

        //        var delay = AsyncOp;
        //        var asyncMessage = new AsyncMessage {Delay = delay};
        //        var messageIn = (Message) Message.CreateFlowStartMessage(asyncMessage);
        //        var callbackReceiver = Guid.NewGuid().ToByteArray();
        //        messageIn.RegisterCallbackPoint(callbackReceiver, MessageIdentifier.Create<SimpleMessage>());

        //        actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

        //        (AsyncOpCompletionDelay + AsyncOp).Sleep();

        //        var messageOut = (Message) actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        CollectionAssert.AreEqual(messageIn.CallbackPoint, messageOut.CallbackPoint);
        //        CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
        //    }
        //    finally
        //    {
        //        actorHost.Stop();
        //    }
        //}

        //[Test]
        //public void IfCallbackIsRegistered_SyncExceptionMessageIsDeliveredToCallbackReceiver()
        //{
        //    var errorMessage = Guid.NewGuid().ToString();

        //    var actorHost = new ActorHost(socketFactory.Object,
        //                                  actorHandlersMap,
        //                                  new AsyncQueue<AsyncMessageContext>(),
        //                                  new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
        //                                  routerConfiguration,
        //                                  securityProvider.Object,
        //                                  performanceCounterManager.Object,
        //                                  logger);
        //    actorHost.AssignActor(new ExceptionActor());
        //    try
        //    {
        //        StartActorHost(actorHost);

        //        var messageIn = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = errorMessage});
        //        var callbackReceiver = Guid.NewGuid().ToByteArray();
        //        var callbackPoints = new[] {MessageIdentifier.Create<SimpleMessage>(), KinoMessages.Exception};
        //        messageIn.RegisterCallbackPoint(callbackReceiver, callbackPoints);

        //        var socket = actorHostSocketFactory.GetRoutableSocket();
        //        socket.DeliverMessage(messageIn);

        //        var messageOut = (Message) socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(errorMessage, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
        //        Assert.IsTrue(messageOut.Equals(KinoMessages.Exception));
        //        CollectionAssert.Contains(messageOut.CallbackPoint, KinoMessages.Exception);
        //        CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
        //    }
        //    finally
        //    {
        //        actorHost.Stop();
        //    }
        //}

        //[Test]
        //public void IfCallbackIsRegistered_AsyncExceptionMessageIsDeliveredToCallbackReceiver()
        //{
        //    var actorHost = new ActorHost(socketFactory.Object,
        //                                  actorHandlersMap,
        //                                  new AsyncQueue<AsyncMessageContext>(),
        //                                  new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
        //                                  routerConfiguration,
        //                                  securityProvider.Object,
        //                                  performanceCounterManager.Object,
        //                                  logger);
        //    actorHost.AssignActor(new ExceptionActor());
        //    try
        //    {
        //        StartActorHost(actorHost);

        //        var error = Guid.NewGuid().ToString();
        //        var asyncMessage = new AsyncExceptionMessage
        //                           {
        //                               Delay = AsyncOp,
        //                               ErrorMessage = error
        //                           };
        //        var messageIn = (Message) Message.CreateFlowStartMessage(asyncMessage);
        //        var callbackReceiver = Guid.NewGuid().ToByteArray();
        //        var callbackPoints = new[] {MessageIdentifier.Create<SimpleMessage>(), KinoMessages.Exception};
        //        messageIn.RegisterCallbackPoint(callbackReceiver, callbackPoints);

        //        actorHostSocketFactory.GetRoutableSocket().DeliverMessage(messageIn);

        //        (AsyncOpCompletionDelay + AsyncOp).Sleep();

        //        var messageOut = (Message) actorHostSocketFactory.GetAsyncCompletionSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(error, messageOut.GetPayload<ExceptionMessage>().Exception.Message);
        //        Assert.IsTrue(messageOut.Equals(KinoMessages.Exception));
        //        CollectionAssert.Contains(messageOut.CallbackPoint, KinoMessages.Exception);
        //        CollectionAssert.AreEqual(messageIn.CallbackReceiverIdentity, messageOut.CallbackReceiverIdentity);
        //    }
        //    finally
        //    {
        //        actorHost.Stop();
        //    }
        //}

        //[Test]
        //public void ExceptionMessage_HasDomainSet()
        //{
        //    var kinoDomain = Guid.NewGuid().ToString();
        //    securityProvider.Setup(m => m.GetDomain(KinoMessages.Exception.Identity)).Returns(kinoDomain);

        //    var actorHost = new ActorHost(socketFactory.Object,
        //                                  actorHandlersMap,
        //                                  new AsyncQueue<AsyncMessageContext>(),
        //                                  new AsyncQueue<IEnumerable<ActorMessageHandlerIdentifier>>(),
        //                                  routerConfiguration,
        //                                  securityProvider.Object,
        //                                  performanceCounterManager.Object,
        //                                  logger);
        //    actorHost.AssignActor(new ExceptionActor());
        //    try
        //    {
        //        StartActorHost(actorHost);

        //        var messageIn = Message.CreateFlowStartMessage(new SimpleMessage());
        //        var socket = actorHostSocketFactory.GetRoutableSocket();
        //        socket.DeliverMessage(messageIn);

        //        var messageOut = socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(kinoDomain, messageOut.Domain);
        //    }
        //    finally
        //    {
        //        actorHost.Stop();
        //    }
        //}

        private static bool IsAsyncMessage(AsyncMessageContext amc)
            => amc.OutMessages.First().Equals(MessageIdentifier.Create<AsyncMessage>());

        private static void StartActorHost(IActorHost actorHost)
        {
            actorHost.Start();
            AsyncOpCompletionDelay.Sleep();
        }

        private void WaitUntilResponseSent(Mock<ILocalSocket<IMessage>> mock)
            => WaitUntilResponseSent(mock, _ => true);

        private void WaitUntilResponseSent(Mock<ILocalSocket<IMessage>> mock, Func<IMessage, bool> predicate)
        {
            var retryCount = 3;
            Exception error = null;
            do
            {
                AsyncOp.Sleep();
                try
                {
                    mock.Verify(m => m.Send(It.Is<IMessage>(msg => predicate(msg))), Times.AtLeastOnce());
                }
                catch (Exception err)
                {
                    error = err;
                }
            } while (--retryCount > 0 && error != null);

            if (error != null)
            {
                throw error;
            }
        }

        private void SetupMessageSend(Mock<ILocalSocket<IMessage>> mock, IMessage messageIn)
        {
            var waitHandle = new AutoResetEvent(true);
            mock.Setup(m => m.CanReceive()).Returns(waitHandle);
            mock.Setup(m => m.TryReceive()).Returns(messageIn);
        }
    }
}