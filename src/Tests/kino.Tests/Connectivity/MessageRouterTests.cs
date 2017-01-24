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
        private static readonly TimeSpan ReceiveMessageDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan ReceiveMessageCompletionDelay = ReceiveMessageDelay + TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan AsyncOpCompletionDelay = TimeSpan.FromMilliseconds(100);        
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
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<IExternalRoutingTable> externalRoutingTable;

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
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
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
            AsyncOpCompletionDelay.Sleep();
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
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        [Test]
        public void IfLocalRouterSocketReceivesNullMessage_ServiceMessageHandlersAreNotCalled()
        {
            localRouterSocket.SetupMessageReceived(null, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.IsAny<MessageIdentifier>()), Times.Never);
        }

        [Test]
        public void IfMessageProcessedByServiceMessageHandler_InternalRoutingTableIsNotLookedUp()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object);
            var message = Message.Create(new DiscoverMessageRouteMessage()).As<Message>();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandlerRegistry.Setup(m => m.GetMessageHandler(message)).Returns(serviceMessageHandler.Object);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>()), Times.Never);
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        [Test]
        public void IfInternalRegistrationMessageIsReceived_InternalRegistrationHandlerIsCalled()
        {
            var internalRouteRegistration = new InternalRouteRegistration();
            internalRegistrationsReceiver.Setup(m => m.TryReceive()).Returns(() =>
                                                                             {
                                                                                 internalRegistrationsReceiverWaitHandle.Reset();
                                                                                 return internalRouteRegistration;
                                                                             });
            messageRouter.Start();
            //
            internalRegistrationsReceiverWaitHandle.Set();
            AsyncOpCompletionDelay.Sleep();
            //
            internalRegistrationHandler.Verify(m => m.Handle(internalRouteRegistration), Times.Once);
        }

        [Test]
        public void IfInternalRegistrationMessageIsNull_InternalRegistrationHandlerIsNotCalled()
        {
            var internalRouteRegistration = new InternalRouteRegistration();
            internalRegistrationsReceiver.Setup(m => m.TryReceive()).Returns(() =>
                                                                             {
                                                                                 internalRegistrationsReceiverWaitHandle.Reset();
                                                                                 return null;
                                                                             });
            messageRouter.Start();
            //
            internalRegistrationsReceiverWaitHandle.Set();
            //
            internalRegistrationHandler.Verify(m => m.Handle(internalRouteRegistration), Times.Never);
        }

        [Test]
        public void IfMessageIsNotProcessedByServiceMessageHandler_InternalRoutingTableIsLookedUp()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            serviceMessageHandlerRegistry.Setup(m => m.GetMessageHandler(message)).Returns((IServiceMessageHandler) null);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        [Test]
        public void IfMessageCameFromLocalActor_ItIsCloned()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage());
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            localSocket.Verify(m => m.Send(It.Is<IMessage>(msg => !ReferenceEquals(msg, message))), Times.Once);
        }

        [Test]
        public void IfMessageCameFromOtherNode_ItIsNotCloned()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            message.AddHop();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            localSocket.Verify(m => m.Send(It.Is<IMessage>(msg => ReferenceEquals(msg, message))), Times.Once);
        }

        [Test]
        public void BrodcastMessageFromLocalActor_IsSentToLocalAndRemoteActors()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] { localSocket.Object };
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage(), DistributionPattern.Broadcast).As<Message>();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
        }

        [Test]
        public void BrodcastMessageFromRemoteActor_IsSentOnlyToLocalActors()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] { localSocket.Object };
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage(), DistributionPattern.Broadcast).As<Message>();
            message.AddHop();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Never);
        }

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