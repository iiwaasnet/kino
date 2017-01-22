using System;
using System.Threading;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class MessageRouterTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromSeconds(2);
        private Mock<ISocketFactory> socketFactory;
        private Mock<ILogger> logger;
        private Mock<IClusterHealthMonitor> clusterHealthMonitor;
        private Mock<IInternalMessageRouteRegistrationHandler> internalRegistrationHandler;
        private Mock<ILocalReceivingSocket<InternalRouteRegistration>> internalRegistrationsReceiver;
        private Mock<IScaleOutConfigurationProvider> scaleOutConfigurationProvider;
        private Mock<IClusterServices> clusterServices;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> perfCounterManager;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ILocalSocket<IMessage>> localRouterSocket;
        private MessageRouter messageRouter;
        private ManualResetEventSlim localRouterSocketWaitHandle;
        private ManualResetEventSlim internalRegistrationsReceiverWaitHandle;
        private Mock<ISocket> routerSocket;
        private Mock<IPerformanceCounter> perfCounter;
        private Mock<IServiceMessageHandlerRegistry> serviceMessageHandlerRegistry;

        [SetUp]
        public void Setup()
        {
            socketFactory = new Mock<ISocketFactory>();
            routerSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(routerSocket.Object);
            logger = new Mock<ILogger>();
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            var socketEndpoint = new SocketEndpoint("tcp://127.0.0.1:8080", Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(socketEndpoint);
            clusterServices = new Mock<IClusterServices>();
            serviceMessageHandlerRegistry = new Mock<IServiceMessageHandlerRegistry>();
            serviceMessageHandlerRegistry.Setup(m => m.GetMessageHandler(It.IsAny<MessageIdentifier>())).Returns((IServiceMessageHandler) null);
            perfCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            perfCounter = new Mock<IPerformanceCounter>();
            perfCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            securityProvider = new Mock<ISecurityProvider>();
            localRouterSocket = new Mock<ILocalSocket<IMessage>>();
            localRouterSocketWaitHandle = new ManualResetEventSlim(false);
            localRouterSocket.Setup(m => m.CanReceive()).Returns(localRouterSocketWaitHandle.WaitHandle);
            internalRegistrationsReceiver = new Mock<ILocalReceivingSocket<InternalRouteRegistration>>();
            internalRegistrationsReceiverWaitHandle = new ManualResetEventSlim(false);
            internalRegistrationsReceiver.Setup(m => m.CanReceive()).Returns(internalRegistrationsReceiverWaitHandle.WaitHandle);
            internalRegistrationHandler = new Mock<IInternalMessageRouteRegistrationHandler>();
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            messageRouter = CreateMessageRouter();
        }

        [Test]
        public void StartMessageRouter_StartsClusterServices()
        {
            messageRouter.Start();
            //
            clusterServices.Verify(m => m.StartClusterServices(), Times.Once);
            scaleOutConfigurationProvider.Verify(m => m.GetScaleOutAddress(), Times.Once);
        }

        [Test]
        public void WhenMessageRouterStarts_SocketWaitHandlesAreRetreived()
        {
            messageRouter.Start();
            //
            localRouterSocket.Verify(m => m.CanReceive(), Times.Once);
            internalRegistrationsReceiver.Verify(m => m.CanReceive(), Times.Once);
        }

        [Test]
        public void WhenMessageRouterStarts_ScaleOutBackendSocketIsCreated()
        {
            messageRouter.Start();
            //
            socketFactory.Verify(m => m.CreateRouterSocket(), Times.Once);
        }

        [Test]
        public void IfLocalRouterSocketIsReadyToReceive_ItsTryReceiveMethidIsCalled()
        {
            localRouterSocket.Setup(m => m.TryReceive()).Returns(() =>
                                                                 {
                                                                     localRouterSocketWaitHandle.Reset();
                                                                     return null;
                                                                 });
            messageRouter.Start();
            //
            localRouterSocketWaitHandle.Set();
            //
            localRouterSocket.Verify(m => m.TryReceive(), Times.Once);
            internalRegistrationsReceiver.Verify(m => m.TryReceive(), Times.Never);
        }

        [Test]
        public void IfInternalRegistrationsReceiverIsReadyToReceive_ItsTryReceiveMethidIsCalled()
        {
            internalRegistrationsReceiver.Setup(m => m.TryReceive()).Returns(() =>
                                                                             {
                                                                                 internalRegistrationsReceiverWaitHandle.Reset();
                                                                                 return null;
                                                                             });
            messageRouter.Start();
            //
            internalRegistrationsReceiverWaitHandle.Set();
            //
            internalRegistrationsReceiver.Verify(m => m.TryReceive(), Times.Once);
            localRouterSocket.Verify(m => m.TryReceive(), Times.Never);
        }

        [Test]
        public void ReceivedOverLocalRouterSocketMessage_AlwaysPassedToServiceMessageHandlers()
        {
            var message = Message.Create(new SimpleMessage());
            localRouterSocket.SetupMessageReceived(message, AsyncOpCompletionDelay);
            //
            messageRouter.Start();
            AsyncOpCompletionDelay.Sleep();
            //
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        [Test]
        public void IfMessageProcessedByServiceMessageHandler_InternalRoutingTableIsNotLookedUp()
        {
            var internalRoutingTable = new Mock<IInternalRoutingTable>();
            messageRouter = CreateMessageRouter(internalRoutingTable.Object);
            var message = Message.Create(new DiscoverMessageRouteMessage()).As<Message>();
            localRouterSocket.SetupMessageReceived(message, AsyncOpCompletionDelay);
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandlerRegistry.Setup(m => m.GetMessageHandler(message)).Returns(serviceMessageHandler.Object);
            //
            messageRouter.Start();
            AsyncOpCompletionDelay.Sleep();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>()), Times.Never);
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        //[Test]
        //public void RegisterLocalMessageHandlers_AddsActorIdentifier()
        //{
        //    var internalRoutingTable = new InternalRoutingTable();
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
        //                                                                             internalRoutingTable,
        //                                                                             securityProvider.Object,
        //                                                                             logger.Object)
        //                             };

        //    var router = CreateMessageRouter(internalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentity = Guid.NewGuid().ToByteArray();
        //        var version = Guid.NewGuid().ToByteArray();
        //        var socketIdentity = Guid.NewGuid().ToByteArray();
        //        var partition = Guid.NewGuid().ToByteArray();
        //        var message = Message.Create(new RegisterInternalMessageRouteMessage
        //                                     {
        //                                         SocketIdentity = socketIdentity,
        //                                         LocalMessageContracts = new[]
        //                                                                 {
        //                                                                     new MessageContract
        //                                                                     {
        //                                                                         Identity = messageIdentity,
        //                                                                         Version = version,
        //                                                                         Partition = partition
        //                                                                     }
        //                                                                 }
        //                                     });
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOpCompletionDelay.Sleep();

        //        var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, partition));

        //        Assert.IsNotNull(identifier);
        //        Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
        //        CollectionAssert.AreEqual(socketIdentity, identifier.Identity);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void LocalActorRegistrations_AreNotBroadcastedToOtherNodes()
        //{
        //    var internalRoutingTable = new InternalRoutingTable();
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
        //                                                                             internalRoutingTable,
        //                                                                             securityProvider.Object,
        //                                                                             logger.Object)
        //                             };

        //    var router = CreateMessageRouter(internalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentity = Guid.NewGuid().ToByteArray();
        //        var version = Guid.NewGuid().ToByteArray();
        //        var socketIdentity = Guid.NewGuid().ToByteArray();
        //        var partition = Guid.NewGuid().ToByteArray();
        //        var message = Message.Create(new RegisterInternalMessageRouteMessage
        //                                     {
        //                                         SocketIdentity = socketIdentity,
        //                                         LocalMessageContracts = new[]
        //                                                                 {
        //                                                                     new MessageContract
        //                                                                     {
        //                                                                         Identity = messageIdentity,
        //                                                                         Version = version,
        //                                                                         Partition = partition
        //                                                                     }
        //                                                                 }
        //                                     },
        //                                     domain);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOpCompletionDelay.Sleep();

        //        var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, partition));

        //        Assert.IsNotNull(identifier);

        //        clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), domain), Times.Never);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void RegisterGlobalMessageHandlers_AddsActorIdentifier()
        //{
        //    var internalRoutingTable = new InternalRoutingTable();
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
        //                                                                             internalRoutingTable,
        //                                                                             securityProvider.Object,
        //                                                                             logger.Object)
        //                             };

        //    var router = CreateMessageRouter(internalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentity = Guid.NewGuid().ToByteArray();
        //        var version = Guid.NewGuid().ToByteArray();
        //        var socketIdentity = Guid.NewGuid().ToByteArray();
        //        var partition = Guid.NewGuid().ToByteArray();
        //        var message = Message.Create(new RegisterInternalMessageRouteMessage
        //                                     {
        //                                         SocketIdentity = socketIdentity,
        //                                         GlobalMessageContracts = new[]
        //                                                                  {
        //                                                                      new MessageContract
        //                                                                      {
        //                                                                          Identity = messageIdentity,
        //                                                                          Version = version,
        //                                                                          Partition = partition
        //                                                                      }
        //                                                                  }
        //                                     });
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOpCompletionDelay.Sleep();

        //        var identifier = internalRoutingTable.FindRoute(new MessageIdentifier(version, messageIdentity, partition));

        //        Assert.IsNotNull(identifier);
        //        Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
        //        CollectionAssert.AreEqual(socketIdentity, identifier.Identity);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void HandlerForReceiverIdentity_HasHighestPriority()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();

        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = (Message) SendMessageOverMessageHub();

        //        var callbackSocketIdentity = message.CallbackReceiverIdentity;
        //        var callbackIdentifier = new MessageIdentifier(IdentityExtensions.Empty, callbackSocketIdentity, IdentityExtensions.Empty);
        //        internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(callbackIdentifier))))
        //                            .Returns(new SocketIdentifier(callbackSocketIdentity));

        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(callbackIdentifier))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void MessageIsRouted_BasedOnHandlerIdentities()
        //{
        //    var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
        //    var partition = Guid.NewGuid().ToByteArray();
        //    var actorIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
        //                        .Returns(actorSocketIdentity);

        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = Message.Create(new SimpleMessage {Partition = partition});
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOpCompletionDelay.Sleep();

        //        internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void MessageFromOnePartition_IsNotRoutedToActorFromOtherPartition()
        //{
        //    var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
        //    var actorPartition = Guid.NewGuid().ToByteArray();
        //    var actorIdentifier = MessageIdentifier.Create<SimpleMessage>(actorPartition);
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
        //                        .Returns(actorSocketIdentity);

        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messagePartition = Guid.NewGuid().ToByteArray();
        //        var message = Message.Create(new SimpleMessage {Partition = messagePartition});
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOpCompletionDelay.Sleep();

        //        internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Never);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void MessageIsRouted_BasedOnHandler_Identity_Version_Partition()
        //{
        //    var actorSocketIdentity = new SocketIdentifier(Guid.NewGuid().ToString().GetBytes());
        //    var partition = Guid.NewGuid().ToByteArray();
        //    var actorIdentifier = MessageIdentifier.Create<SimpleMessage>(partition);
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))))
        //                        .Returns(actorSocketIdentity);

        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = Message.Create(new SimpleMessage {Partition = partition});
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOpCompletionDelay.Sleep();

        //        internalRoutingTable.Verify(m => m.FindRoute(It.Is<MessageIdentifier>(mhi => mhi.Equals(actorIdentifier))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfLocalRoutingTableHasNoMessageHandlerRegistrationAndMessageDomainIsAllowed_MessageRoutedToOtherNodes()
        //{
        //    var message = Message.Create(new SimpleMessage(), domain);
        //    var messageOut = TestLocalRoutingTableHasNoMessageHandlerRegistration(message);

        //    Assert.AreEqual(message, messageOut);
        //}

        //[Test]
        //public void IfLocalRoutingTableHasNoMessageHandlerRegistrationAndMessageDomainIsNotAllowed_ThrowsSecurityExceptionAndMessageNotRoutedToOtherNodes()
        //{
        //    var message = Message.Create(new SimpleMessage(), Guid.NewGuid().ToString());

        //    Assert.Throws<InvalidOperationException>(() => TestLocalRoutingTableHasNoMessageHandlerRegistration(message));
        //}

        //private IMessage TestLocalRoutingTableHasNoMessageHandlerRegistration(IMessage message)
        //{
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>(), null))
        //                        .Returns(new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())});

        //    var router = CreateMessageRouter(null, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        return messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfReceivingNodeIsSetForMessageFromAllowedDomain_MessageAlwaysRoutedToOtherNodes()
        //{
        //    var message = Message.Create(new SimpleMessage(), domain);
        //    var messageOut = TestMessageAlwaysRoutedToReceivingNode(message);

        //    Assert.AreEqual(message, messageOut);
        //}

        //[Test]
        //public void IfReceivingNodeIsSetForMessageFromNotAllowedDomain_MessageNotRoutedToOtherNodes()
        //{
        //    var message = Message.Create(new SimpleMessage(), Guid.NewGuid().ToString());
        //    Assert.Throws<InvalidOperationException>(() => TestMessageAlwaysRoutedToReceivingNode(message));
        //}

        //private IMessage TestMessageAlwaysRoutedToReceivingNode(IMessage message)
        //{
        //    var externalNode = new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())};
        //    message.SetReceiverNode(new SocketIdentifier(externalNode.Node.SocketIdentity));

        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m =>
        //                               m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi)), It.Is<byte[]>(id => Unsafe.Equals(id, externalNode.Node.SocketIdentity))))
        //                        .Returns(externalNode);
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi))))
        //                        .Returns(SocketIdentifier.Create());

        //    var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        return messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfReceivingNodeIsSetAndExternalRouteIsNotRegistered_RouterRequestsDiscovery()
        //{
        //    var externalNode = new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())};
        //    var message = Message.Create(new SimpleMessage());
        //    message.SetReceiverNode(new SocketIdentifier(externalNode.Node.SocketIdentity));

        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>(), It.IsAny<byte[]>()))
        //                        .Returns((PeerConnection) null);
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => message.Equals(mi))))
        //                        .Returns(SocketIdentifier.Create());

        //    var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        clusterMonitor.Verify(m => m.UnregisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>()), Times.Never());
        //        clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => message.Equals(id))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void MessageHopIsAdded_WhenMessageIsSentToOtherNode()
        //{
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m => m.FindRoute(It.IsAny<MessageIdentifier>(), null))
        //                        .Returns(new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())});

        //    var router = CreateMessageRouter(null, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = Message.CreateFlowStartMessage(new SimpleMessage(), domain);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);
        //        Assert.AreEqual(0, message.Hops);

        //        var messageOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(1, messageOut.Hops);
        //        Assert.IsTrue(Unsafe.Equals(message.CorrelationId, messageOut.CorrelationId));
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void BroadcastMessage_IsRoutedToAllLocalAndRemoteActors()
        //{
        //    var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
        //                        .Returns(new[] {new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())}});
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
        //                        .Returns(new[] {new SocketIdentifier(Guid.NewGuid().ToByteArray())});

        //    var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = Message.Create(new SimpleMessage(), domain, DistributionPattern.Broadcast);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        var messageScaleOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        //        var messageLocalOut = messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(message, messageScaleOut);
        //        Assert.AreEqual(message, messageLocalOut);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void BroadcastMessageIsRoutedOnlyToLocalActors_IfHopsCountGreaterThanZero()
        //{
        //    var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
        //                        .Returns(new[] {new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())}});
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    internalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
        //                        .Returns(new[] {new SocketIdentifier(Guid.NewGuid().ToByteArray())});

        //    var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = (Message) Message.Create(new SimpleMessage(), DistributionPattern.Broadcast);
        //        message.AddHop();
        //        Assert.AreEqual(1, message.Hops);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));
        //        var messageLocalOut = messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(message, messageLocalOut);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void BroadcastMessage_IsRoutedToRemoteActorsEvenIfNoLocalActorsRegistered()
        //{
        //    var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    externalRoutingTable.Setup(m => m.FindAllRoutes(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier))))
        //                        .Returns(new[] {new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity())}});
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();

        //    var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = Message.Create(new SimpleMessage(), domain, DistributionPattern.Broadcast);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        var messageScaleOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        //        Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));

        //        Assert.AreEqual(message, messageScaleOut);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void BroadcastMessageIsNotRoutedAndRouteDiscoverRequestSent_IfNoLocalActorsRegisteredAndHopsCountGreaterThanZero()
        //{
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();

        //    var router = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = (Message) Message.Create(new SimpleMessage(), DistributionPattern.Broadcast);
        //        message.AddHop();
        //        Assert.AreEqual(1, message.Hops);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));
        //        Assert.Throws<InvalidOperationException>(() => messageRouterSocketFactory.GetRouterSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay));

        //        clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => id.Equals(MessageIdentifier.Create<SimpleMessage>()))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void PeerNodeIsConnected_WhenMessageIsForwardedToIt()
        //{
        //    var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //    var externalRoutingTable = new Mock<IExternalRoutingTable>();
        //    var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1", SocketIdentifier.CreateIdentity()), Connected = false};
        //    externalRoutingTable.Setup(m => m.FindRoute(It.Is<MessageIdentifier>(mi => mi.Equals(messageIdentifier)), null)).Returns(peerConnection);

        //    var router = CreateMessageRouter(null, externalRoutingTable.Object);
        //    try
        //    {
        //        StartMessageRouter(router);
        //        Assert.IsFalse(peerConnection.Connected);
        //        var socket = messageRouterSocketFactory.GetScaleoutBackendSocket();
        //        Assert.IsFalse(socket.IsConnected());
        //        var message = Message.Create(new SimpleMessage(), domain);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        var messageOut = messageRouterSocketFactory.GetScaleoutBackendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.IsTrue(peerConnection.Connected);
        //        Assert.IsTrue(socket.IsConnected());
        //        Assert.AreEqual(message, messageOut);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void MessageReceivedFromOtherNode_ForwardedToLocalRouterSocket()
        //{
        //    var messageSignature = Guid.NewGuid().ToByteArray();
        //    TestMessageReceivedFromOtherNode(messageSignature, messageSignature, MessageIdentifier.Create<SimpleMessage>());
        //}

        //[Test]
        //public void MessageReceivedFromOtherNodeNotForwardedToLocalRouterSocket_IfMessageSignatureDoesntMatch()
        //{
        //    TestMessageReceivedFromOtherNode(Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray(), KinoMessages.Exception);
        //}

        //private void TestMessageReceivedFromOtherNode(byte[] messageSignature, byte[] generatedSignature, MessageIdentifier expected)
        //{
        //    var router = CreateMessageRouter();
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var message = Message.Create(new SimpleMessage(), domain);
        //        securityProvider.Setup(m => m.CreateSignature(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(messageSignature);
        //        message.As<Message>().SignMessage(securityProvider.Object);

        //        securityProvider.Setup(m => m.CreateSignature(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(generatedSignature);
        //        messageRouterSocketFactory.GetScaleoutFrontendSocket().DeliverMessage(message);

        //        var messageOut = messageRouterSocketFactory.GetScaleoutFrontendSocket().GetSentMessages().BlockingLast(AsyncOpCompletionDelay);

        //        Assert.AreEqual(expected, new MessageIdentifier(messageOut));
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfUnhandledMessageReceivedFromOtherNode_RouterUnregistersSelfAndRequestsDiscovery()
        //{
        //    var router = CreateMessageRouter();
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //        var message = (Message) Message.Create(new SimpleMessage());
        //        message.AddHop();
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        clusterMonitor.Verify(m => m.UnregisterSelf(It.Is<IEnumerable<MessageIdentifier>>(ids => ids.First().Equals(messageIdentifier))), Times.Once());
        //        clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => id.Equals(messageIdentifier))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfUnhandledMessageReceivedFromLocalActor_RouterRequestsDiscovery()
        //{
        //    var router = CreateMessageRouter();
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //        var message = Message.Create(new SimpleMessage());
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        clusterMonitor.Verify(m => m.UnregisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>()), Times.Never());
        //        clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageIdentifier>(id => id.Equals(messageIdentifier))), Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfMessageRouterCannotHandleMessage_SelfRegisterIsNotCalled()
        //{
        //    var internalRoutingTable = new InternalRoutingTable();
        //    var router = CreateMessageRouter(internalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //        var message = Message.Create(new DiscoverMessageRouteMessage
        //                                     {
        //                                         MessageContract = new MessageContract
        //                                                           {
        //                                                               Version = messageIdentifier.Version,
        //                                                               Identity = messageIdentifier.Identity,
        //                                                               Partition = messageIdentifier.Partition
        //                                                           }
        //                                     },
        //                                     domain);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        Assert.IsFalse(internalRoutingTable.CanRouteMessage(messageIdentifier));
        //        clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), domain), Times.Never());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfMessageRouterCanHandleMessageBeingDiscovered_SelfRegisterIsCalled()
        //{
        //    var internalRoutingTable = new InternalRoutingTable();
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new MessageRouteDiscoveryHandler(clusterMonitorProvider.Object,
        //                                                                  internalRoutingTable,
        //                                                                  securityProvider.Object,
        //                                                                  logger.Object)
        //                             };

        //    var router = CreateMessageRouter(internalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //        internalRoutingTable.AddMessageRoute(messageIdentifier, SocketIdentifier.Create());
        //        var message = Message.Create(new DiscoverMessageRouteMessage
        //                                     {
        //                                         MessageContract = new MessageContract
        //                                                           {
        //                                                               Version = messageIdentifier.Version,
        //                                                               Identity = messageIdentifier.Identity,
        //                                                               Partition = messageIdentifier.Partition
        //                                                           }
        //                                     },
        //                                     domain);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        Assert.IsTrue(internalRoutingTable.CanRouteMessage(messageIdentifier));
        //        clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageIdentifier>>(ids => ids.First().Equals(messageIdentifier)),
        //                                                  domain),
        //                              Times.Once());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfRegisterExternalMessageRouteMessageReceived_AllRoutesAreAddedToExternalRoutingTable()
        //{
        //    var externalRoutingTable = new ExternalRoutingTable(logger.Object);
        //    var config = new RouterConfiguration {DeferPeerConnection = true};
        //    var routerConfigurationProvider = new Mock<IRouterConfigurationProvider>();
        //    routerConfigurationProvider.Setup(m => m.GetRouterConfiguration()).Returns(config);
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
        //                                                                             clusterMembership.Object,
        //                                                                             routerConfigurationProvider.Object,
        //                                                                             securityProvider.Object,
        //                                                                             logger.Object)
        //                             };

        //    var router = CreateMessageRouter(null, externalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifiers = new[]
        //                                 {
        //                                     MessageIdentifier.Create<SimpleMessage>(),
        //                                     MessageIdentifier.Create<AsyncMessage>()
        //                                 };
        //        var socketIdentity = SocketIdentifier.CreateIdentity();
        //        var message = Message.Create(new RegisterExternalMessageRouteMessage
        //                                     {
        //                                         Uri = "tcp://127.0.0.1:8000",
        //                                         SocketIdentity = socketIdentity,
        //                                         MessageContracts = messageIdentifiers.Select(mi => new MessageContract
        //                                                                                            {
        //                                                                                                Version = mi.Version,
        //                                                                                                Identity = mi.Identity,
        //                                                                                                Partition = mi.Partition
        //                                                                                            }).ToArray()
        //                                     },
        //                                     domain);
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        Assert.IsTrue(Unsafe.Equals(socketIdentity, externalRoutingTable.FindRoute(messageIdentifiers.First(), null).Node.SocketIdentity));
        //        Assert.IsTrue(Unsafe.Equals(socketIdentity, externalRoutingTable.FindRoute(messageIdentifiers.Second(), null).Node.SocketIdentity));
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfUnregisterMessageRouteMessage_RoutesAreRemovedFromExternalRoutingTable()
        //{
        //    var externalRoutingTable = new ExternalRoutingTable(logger.Object);
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new MessageRouteUnregistrationHandler(externalRoutingTable,
        //                                                                       securityProvider.Object,
        //                                                                       logger.Object)
        //                             };

        //    var router = CreateMessageRouter(null, externalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifiers = new[]
        //                                 {
        //                                     MessageIdentifier.Create<SimpleMessage>(),
        //                                     MessageIdentifier.Create<AsyncMessage>()
        //                                 };

        //        var socketIdentity = SocketIdentifier.Create();
        //        var uri = new Uri("tcp://127.0.0.1:8000");
        //        messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
        //        var message = Message.Create(new UnregisterMessageRouteMessage
        //                                     {
        //                                         Uri = uri.ToSocketAddress(),
        //                                         SocketIdentity = socketIdentity.Identity,
        //                                         MessageContracts = messageIdentifiers.Select(mi => new MessageContract
        //                                                                                            {
        //                                                                                                Version = mi.Version,
        //                                                                                                Identity = mi.Identity,
        //                                                                                                Partition = mi.Partition
        //                                                                                            }).ToArray()
        //                                     },
        //                                     domain);

        //        CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        CollectionAssert.IsEmpty(externalRoutingTable.GetAllRoutes());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void UnregisterMessageRouteMessageForAllowedDomain_DeletesClusterMember()
        //{
        //    TestUnregisterMessageRouteMessage(domain, Times.Once());
        //}

        //[Test]
        //public void UnregisterMessageRouteMessageForNotAllowedDomain_DoesntDeletesClusterMember()
        //{
        //    TestUnregisterMessageRouteMessage(Guid.NewGuid().ToString(), Times.Never());
        //}

        //private void TestUnregisterMessageRouteMessage(string securityDomain, Times times)
        //{
        //    var externalRoutingTable = new ExternalRoutingTable(logger.Object);
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new NodeUnregistrationHandler(externalRoutingTable,
        //                                                               clusterMembership.Object,
        //                                                               securityProvider.Object)
        //                             };

        //    var router = CreateMessageRouter(null, externalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var socket = messageRouterSocketFactory.GetRouterSocket();
        //        var message = new UnregisterNodeMessage
        //                      {
        //                          Uri = "tcp://127.0.0.1:5000",
        //                          SocketIdentity = SocketIdentifier.CreateIdentity()
        //                      };
        //        socket.DeliverMessage(Message.Create(message, securityDomain));
        //        AsyncOp.Sleep();

        //        clusterMembership.Verify(m => m.DeleteClusterMember(It.Is<SocketEndpoint>(e => e.Uri.ToSocketAddress() == message.Uri
        //                                                                                       && Unsafe.Equals(e.Identity, message.SocketIdentity))),
        //                                 times);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfAllMessageRoutesUnregisteredForNode_SocketIsDisconnected()
        //{
        //    var externalRoutingTable = new ExternalRoutingTable(logger.Object);
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new MessageRouteUnregistrationHandler(externalRoutingTable,
        //                                                                       securityProvider.Object,
        //                                                                       logger.Object)
        //                             };

        //    var router = CreateMessageRouter(null, externalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifiers = new[]
        //                                 {
        //                                     MessageIdentifier.Create<SimpleMessage>(),
        //                                     MessageIdentifier.Create<AsyncMessage>()
        //                                 };

        //        var socketIdentity = SocketIdentifier.Create();
        //        var uri = new Uri("tcp://127.0.0.1:8000");
        //        messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
        //        var peerConnection = externalRoutingTable.FindRoute(messageIdentifiers.First(), null);
        //        peerConnection.Connected = true;
        //        var backEndScoket = messageRouterSocketFactory.GetScaleoutBackendSocket();
        //        backEndScoket.Connect(uri);
        //        Assert.IsTrue(backEndScoket.IsConnected());
        //        var message = Message.Create(new UnregisterMessageRouteMessage
        //                                     {
        //                                         Uri = uri.ToSocketAddress(),
        //                                         SocketIdentity = socketIdentity.Identity,
        //                                         MessageContracts = messageIdentifiers.Select(mi => new MessageContract
        //                                                                                            {
        //                                                                                                Version = mi.Version,
        //                                                                                                Identity = mi.Identity,
        //                                                                                                Partition = mi.Partition
        //                                                                                            }).ToArray()
        //                                     },
        //                                     domain);

        //        CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        Assert.IsFalse(backEndScoket.IsConnected());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfUnregisterNodeMessageArrivesFromAllowedDomain_AndConnectionWasEstablished_SocketIsDisconnected()
        //{
        //    TestUnregisterNodeMessageRouteMessageDisconnectsSocket(domain, false);
        //}

        //[Test]
        //public void IfUnregisterNodeMessageArrivesFromNotAllowedDomain_SocketIsNotDisconnected()
        //{
        //    TestUnregisterNodeMessageRouteMessageDisconnectsSocket(Guid.NewGuid().ToString(), true);
        //}

        //private void TestUnregisterNodeMessageRouteMessageDisconnectsSocket(string domain, bool connected)
        //{
        //    var externalRoutingTable = new ExternalRoutingTable(logger.Object);
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new NodeUnregistrationHandler(externalRoutingTable,
        //                                                               clusterMembership.Object,
        //                                                               securityProvider.Object)
        //                             };

        //    var router = CreateMessageRouter(null, externalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
        //        var socketIdentity = SocketIdentifier.Create();
        //        var uri = new Uri("tcp://127.0.0.1:8000");
        //        externalRoutingTable.AddMessageRoute(messageIdentifier, socketIdentity, uri);
        //        var peerConnection = externalRoutingTable.FindRoute(messageIdentifier, null);
        //        peerConnection.Connected = true;
        //        var backEndScoket = messageRouterSocketFactory.GetScaleoutBackendSocket();
        //        backEndScoket.Connect(uri);
        //        Assert.IsTrue(backEndScoket.IsConnected());
        //        var message = Message.Create(new UnregisterNodeMessage
        //                                     {
        //                                         Uri = uri.ToSocketAddress(),
        //                                         SocketIdentity = socketIdentity.Identity
        //                                     },
        //                                     domain);

        //        CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        Assert.AreEqual(connected, backEndScoket.IsConnected());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfUnregisterNodeMessageArrivesFromAllowedDomain_AllRoutesAreRemovedFromExternalRoutingTable()
        //{
        //    TestUnregisterNodeMessageRemovesRoutesFromExternalRoutingTable(domain, true);
        //}

        //[Test]
        //public void IfUnregisterNodeMessageArrivesFromNotAllowedDomain_RoutesAreNotRemovedFromExternalRoutingTable()
        //{
        //    TestUnregisterNodeMessageRemovesRoutesFromExternalRoutingTable(Guid.NewGuid().ToString(), false);
        //}

        //[Test]
        //public void SetMessageRouterConfigurationActiveIsNotCalled_IfMessageRouterIsNotStarted()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    //
        //    routerConfigurationManager.Verify(m => m.SetMessageRouterConfigurationActive(), Times.Never);
        //}

        //[Test]
        //public void SetActiveScaleOutAddressIsNotCalled_IfMessageRouterIsNotStarted()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    //
        //    routerConfigurationManager.Verify(m => m.SetActiveScaleOutAddress(It.IsAny<SocketEndpoint>()), Times.Never);
        //    routerConfigurationManager.Verify(m => m.GetScaleOutAddressRange(), Times.Never);
        //}

        //[Test]
        //public void SetMessageRouterConfigurationActiveIsCalled_AfterMessageRouterIsStarted()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    try
        //    {
        //        //
        //        StartMessageRouter(router);
        //        //
        //        routerConfigurationManager.Verify(m => m.SetMessageRouterConfigurationActive(), Times.Once);
        //        routerConfigurationManager.Verify(m => m.GetInactiveRouterConfiguration(), Times.Once);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void SetActiveScaleOutAddressIsCalled_AfterMessageRouterIsStarted()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    try
        //    {
        //        //
        //        StartMessageRouter(router);
        //        //
        //        routerConfigurationManager.Verify(m => m.SetActiveScaleOutAddress(scaleOutAddress), Times.Once);
        //        routerConfigurationManager.Verify(m => m.GetScaleOutAddressRange(), Times.Once);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfBindOnScaleOutFrontendSocketThrowsException_ConnectonScaleOutFrontendSocketIsNotCalled()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    messageRouterSocketFactory.SocketCreated += socket => { socket.Setup(m => m.Bind(scaleOutAddress.Uri)).Throws<Exception>(); };
        //    try
        //    {
        //        //
        //        StartMessageRouter(router);
        //        //
        //        routerConfigurationManager.Verify(m => m.GetScaleOutAddressRange(), Times.Once);
        //        routerConfigurationManager.Verify(m => m.SetActiveScaleOutAddress(scaleOutAddress), Times.Never);
        //        var scaleOutFrontEndSocket = messageRouterSocketFactory.GetScaleoutFrontendSocket();
        //        scaleOutFrontEndSocket.Verify(m => m.Connect(routerConfiguration.RouterAddress.Uri), Times.Never);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //[Test]
        //public void IfBindOneScaleOutFrontendSocketThrowsException_BindIsRetriedWithOtherEndpoints()
        //{
        //    var internalRoutingTable = new Mock<IInternalRoutingTable>();
        //    var router = CreateMessageRouter(internalRoutingTable.Object);
        //    var failingScaleOutAddress = new SocketEndpoint("tcp://127.0.0.124:50000");
        //    routerConfigurationManager.Setup(m => m.GetScaleOutAddressRange()).Returns(new[] { failingScaleOutAddress, scaleOutAddress });

        //    messageRouterSocketFactory.SocketCreated += socket => { socket.Setup(m => m.Bind(failingScaleOutAddress.Uri)).Throws<Exception>(); };
        //    try
        //    {
        //        //
        //        StartMessageRouter(router);
        //        //
        //        routerConfigurationManager.Verify(m => m.GetScaleOutAddressRange(), Times.Once);
        //        routerConfigurationManager.Verify(m => m.SetActiveScaleOutAddress(scaleOutAddress), Times.Once);
        //        routerConfigurationManager.Verify(m => m.SetActiveScaleOutAddress(failingScaleOutAddress), Times.Never);
        //        var scaleOutFrontEndSocket = messageRouterSocketFactory.GetScaleoutFrontendSocket();
        //        scaleOutFrontEndSocket.Verify(m => m.Bind(It.IsAny<Uri>()), Times.Exactly(2));
        //        scaleOutFrontEndSocket.Verify(m => m.Connect(routerConfiguration.RouterAddress.Uri), Times.Once);
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //private void TestUnregisterNodeMessageRemovesRoutesFromExternalRoutingTable(string domain, bool externalRoutesEmpty)
        //{
        //    var externalRoutingTable = new ExternalRoutingTable(logger.Object);
        //    serviceMessageHandlers = new[]
        //                             {
        //                                 new NodeUnregistrationHandler(externalRoutingTable,
        //                                                               clusterMembership.Object,
        //                                                               securityProvider.Object)
        //                             };

        //    var router = CreateMessageRouter(null, externalRoutingTable);
        //    try
        //    {
        //        StartMessageRouter(router);

        //        var messageIdentifiers = new[]
        //                                 {
        //                                     MessageIdentifier.Create<SimpleMessage>(),
        //                                     MessageIdentifier.Create<AsyncMessage>()
        //                                 };

        //        var socketIdentity = SocketIdentifier.Create();
        //        var uri = new Uri("tcp://127.0.0.1:8000");
        //        messageIdentifiers.ForEach(mi => externalRoutingTable.AddMessageRoute(mi, socketIdentity, uri));
        //        var message = Message.Create(new UnregisterNodeMessage
        //                                     {
        //                                         Uri = uri.ToSocketAddress(),
        //                                         SocketIdentity = socketIdentity.Identity
        //                                     },
        //                                     domain);

        //        CollectionAssert.IsNotEmpty(externalRoutingTable.GetAllRoutes());
        //        messageRouterSocketFactory.GetRouterSocket().DeliverMessage(message);

        //        AsyncOp.Sleep();

        //        Assert.AreEqual(externalRoutesEmpty, !externalRoutingTable.GetAllRoutes().Any());
        //    }
        //    finally
        //    {
        //        router.Stop();
        //    }
        //}

        //private IMessage SendMessageOverMessageHub()
        //{
        //    var performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
        //    var logger = new Mock<ILogger>();
        //    var socketFactory = new Mock<ISocketFactory>();
        //    var socket = new MockSocket();
        //    socketFactory.Setup(m => m.CreateDealerSocket()).Returns(socket);

        //    var message = Message.CreateFlowStartMessage(new SimpleMessage());
        //    var callback = CallbackPoint.Create<SimpleMessage>();

        //    var messageHub = new MessageHub(socketFactory.Object,
        //                                    new CallbackHandlerStack(),
        //                                    routerConfigurationManager.Object,
        //                                    securityProvider.Object,
        //                                    performanceCounterManager.Object,
        //                                    logger.Object);
        //    messageHub.Start();
        //    messageHub.EnqueueRequest(message, callback);
        //    AsyncOpCompletionDelay.Sleep();

        //    return socket.GetSentMessages().BlockingLast(AsyncOpCompletionDelay);
        //}

        private MessageRouter CreateMessageRouter(IInternalRoutingTable internalRoutingTable = null,
                                                  IExternalRoutingTable externalRoutingTable = null)
            => new MessageRouter(socketFactory.Object,
                                 internalRoutingTable ?? new InternalRoutingTable(),
                                 externalRoutingTable ?? new ExternalRoutingTable(logger.Object),
                                 scaleOutConfigurationProvider.Object,
                                 clusterServices.Object,
                                 serviceMessageHandlerRegistry.Object,
                                 perfCounterManager.Object,
                                 securityProvider.Object,
                                 localRouterSocket.Object,
                                 internalRegistrationsReceiver.Object,
                                 internalRegistrationHandler.Object,
                                 clusterHealthMonitor.Object,
                                 logger.Object);
    }
}