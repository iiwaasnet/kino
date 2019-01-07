using System;
using System.Collections.Generic;
using System.Linq;
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
using NetMQ;
using NUnit.Framework;
using Health = kino.Cluster.Health;
using MessageRoute = kino.Cluster.MessageRoute;

namespace kino.Tests.Routing
{
    public class MessageRouterTests
    {
        private static readonly TimeSpan ReceiveMessageDelay = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan ReceiveMessageCompletionDelay = ReceiveMessageDelay + TimeSpan.FromMilliseconds(3000);
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
        private Mock<ISocket> scaleOutSocket;
        private Mock<IPerformanceCounter> perfCounter;
        private Mock<IServiceMessageHandlerRegistry> serviceMessageHandlerRegistry;
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<IExternalRoutingTable> externalRoutingTable;
        private SocketEndpoint localNodeEndpoint;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IRoundRobinDestinationList> roundRobinDestinationList;
        private Mock<ILocalSocketFactory> localSocketFactory;

        [SetUp]
        public void Setup()
        {
            socketFactory = new Mock<ISocketFactory>();
            scaleOutSocket = new Mock<ISocket>();
            socketFactory.Setup(m => m.CreateRouterSocket()).Returns(scaleOutSocket.Object);
            logger = new Mock<ILogger>();
            scaleOutConfigurationProvider = new Mock<IScaleOutConfigurationProvider>();
            localNodeEndpoint = SocketEndpoint.Parse("tcp://127.0.0.1:8080", Guid.NewGuid().ToByteArray());
            scaleOutConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(localNodeEndpoint);
            clusterServices = new Mock<IClusterServices>();
            serviceMessageHandlerRegistry = new Mock<IServiceMessageHandlerRegistry>();
            serviceMessageHandlerRegistry.Setup(m => m.GetMessageHandler(It.IsAny<MessageIdentifier>())).Returns((IServiceMessageHandler) null);
            perfCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            perfCounter = new Mock<IPerformanceCounter>();
            perfCounterManager.Setup(m => m.GetCounter(It.IsAny<KinoPerformanceCounters>())).Returns(perfCounter.Object);
            securityProvider = new Mock<ISecurityProvider>();
            localRouterSocket = new Mock<ILocalSocket<IMessage>>();
            localRouterSocket.Setup(m => m.CanReceive()).Returns(new ManualResetEventSlim(false).WaitHandle);
            internalRegistrationsReceiver = new Mock<ILocalReceivingSocket<InternalRouteRegistration>>();
            internalRegistrationsReceiver.Setup(m => m.CanReceive()).Returns(new ManualResetEventSlim(false).WaitHandle);
            internalRegistrationHandler = new Mock<IInternalMessageRouteRegistrationHandler>();
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            clusterServices.Setup(m => m.GetClusterHealthMonitor()).Returns(clusterHealthMonitor.Object);
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterServices.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            roundRobinDestinationList = new Mock<IRoundRobinDestinationList>();
            localSocketFactory = new Mock<ILocalSocketFactory>();
            localSocketFactory.Setup(m => m.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket))
                              .Returns(localRouterSocket.Object);
            localSocketFactory.Setup(m => m.CreateNamed<InternalRouteRegistration>(NamedSockets.InternalRegistrationSocket))
                              .Returns((ILocalSocket<InternalRouteRegistration>) internalRegistrationsReceiver.Object);

            messageRouter = CreateMessageRouter();
        }

        [Test]
        public void StartMessageRouter_StartsClusterServices()
        {
            messageRouter.Start();
            messageRouter.Stop();
            //
            clusterServices.Verify(m => m.StartClusterServices(), Times.Once);
            scaleOutConfigurationProvider.Verify(m => m.GetScaleOutAddress(), Times.Once);
        }

        [Test]
        public void WhenMessageRouterStarts_SocketWaitHandlesAreRetrieved()
        {
            messageRouter.Start();
            messageRouter.Stop();
            //
            localRouterSocket.Verify(m => m.CanReceive(), Times.Once);
            internalRegistrationsReceiver.Verify(m => m.CanReceive(), Times.Once);
        }

        [Test]
        public void WhenMessageRouterStarts_ScaleOutBackendSocketIsCreated()
        {
            messageRouter.Start();
            messageRouter.Stop();
            //
            socketFactory.Verify(m => m.CreateRouterSocket(), Times.Once);
        }

        [Test]
        public void IfLocalRouterSocketIsReadyToReceive_ItsTryReceiveMethodIsCalled()
        {
            internalRegistrationsReceiver.Setup(m => m.CanReceive())
                                         .Returns(new ManualResetEventSlim(false).WaitHandle);
            var canReceiveWait = new ManualResetEventSlim(true);
            var tryReceiveWait = new ManualResetEventSlim(false);
            localRouterSocket.Setup(m => m.CanReceive())
                             .Returns(() => canReceiveWait.WaitHandle);
            localRouterSocket.Setup(m => m.TryReceive())
                             .Returns(() => null)
                             .Callback(() =>
                                       {
                                           tryReceiveWait.Set();
                                           canReceiveWait.Reset();
                                       });
            messageRouter.Start();
            //
            tryReceiveWait.Wait();
            messageRouter.Stop();
            //
            localRouterSocket.Verify(m => m.TryReceive(), Times.Once);
            internalRegistrationsReceiver.Verify(m => m.TryReceive(), Times.Never);
        }

        [Test]
        public void IfInternalRegistrationsReceiverIsReadyToReceive_ItsTryReceiveMethodIsCalled()
        {
            var canReceiveWait = new ManualResetEventSlim(true);
            var tryReceiveWait = new ManualResetEventSlim(false);
            internalRegistrationsReceiver.Setup(m => m.CanReceive())
                                         .Returns(() => canReceiveWait.WaitHandle);
            internalRegistrationsReceiver.Setup(m => m.TryReceive())
                                         .Returns(() => null)
                                         .Callback(() =>
                                                   {
                                                       tryReceiveWait.Set();
                                                       canReceiveWait.Reset();
                                                   });
            messageRouter.Start();
            //
            tryReceiveWait.Wait();
            messageRouter.Stop();
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
            messageRouter.Stop();
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
            messageRouter.Stop();
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
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>()), Times.Never);
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        [Test]
        public void IfInternalRegistrationMessageIsReceived_InternalRegistrationHandlerIsCalled()
        {
            var internalRouteRegistration = new InternalRouteRegistration();
            var canReceiveWait = new ManualResetEvent(true);
            var tryReceiveWait = new ManualResetEvent(false);
            internalRegistrationsReceiver.Setup(m => m.CanReceive())
                                         .Returns(() => new ManualResetEventSlim(true).WaitHandle);
            internalRegistrationsReceiver.Setup(m => m.TryReceive())
                                         .Returns(() => internalRouteRegistration)
                                         .Callback(() =>
                                                   {
                                                       tryReceiveWait.Set();
                                                       canReceiveWait.Reset();
                                                   });
            messageRouter.Start();
            //
            tryReceiveWait.WaitOne();
            messageRouter.Stop();
            //
            internalRegistrationHandler.Verify(m => m.Handle(internalRouteRegistration), Times.AtLeastOnce);
        }

        [Test]
        public void IfInternalRegistrationMessageIsNull_InternalRegistrationHandlerIsNotCalled()
        {
            var internalRouteRegistration = new InternalRouteRegistration();
            var canReceiveWait = new ManualResetEventSlim(true);
            var tryReceiveWait = new ManualResetEventSlim(false);
            internalRegistrationsReceiver.Setup(m => m.CanReceive())
                                         .Returns(() => canReceiveWait.WaitHandle);
            internalRegistrationsReceiver.Setup(m => m.TryReceive())
                                         .Returns(() => null)
                                         .Callback(() =>
                                                   {
                                                       canReceiveWait.Reset();
                                                       tryReceiveWait.Set();
                                                   });
            messageRouter.Start();
            //
            tryReceiveWait.Wait();
            messageRouter.Stop();
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
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(id => id.Equals(message))), Times.Once);
        }

        [Test]
        public void IfMessageCameFromLocalActor_ItIsCloned()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.As<IDestination>().Setup(m => m.Equals(localSocket.Object)).Returns(true);
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage());
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
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
            messageRouter.Stop();
            //
            localSocket.Verify(m => m.Send(It.Is<IMessage>(msg => ReferenceEquals(msg, message))), Times.Once);
        }

        [Test]
        public void BroadcastMessageFromLocalActor_IsSentToLocalAndRemoteActors()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage(), DistributionPattern.Broadcast).As<Message>();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
        }

        [Test]
        public void BroadcastMessageFromRemoteActor_IsSentOnlyToLocalActors()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage(), DistributionPattern.Broadcast).As<Message>();
            message.AddHop();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Never);
        }

        [Test]
        public void IfMessageReceiverNodeIdentitySetToLocalNodeIdentity_MessageIsRoutedInternally()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            message.SetReceiverNode(new ReceiverIdentifier(localNodeEndpoint.Identity));
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Never);
        }

        [Test]
        public void IfForMessageInternalAndExternalRoutesExist_FinalDestinationIsSelectedRoundRobin()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(new[] {localSocket.Object});
            var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1:9009", new ReceiverIdentifier(Guid.NewGuid().ToByteArray()).Identity)};
            externalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<ExternalRouteLookupRequest>())).Returns(new[] {peerConnection});
            var message = Message.Create(new SimpleMessage()).As<Message>();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Once);
            roundRobinDestinationList.Verify(m => m.SelectNextDestination(localSocket.Object, peerConnection.Node), Times.Once);
        }

        [Test]
        public void IfMessageReceiverNodeIdentitySetToRemoteNodeIdentity_MessageIsSentToThatNode()
        {
            messageRouter = CreateMessageRouter(internalRoutingTable.Object, externalRoutingTable.Object);
            var localSocket = new Mock<ILocalSocket<IMessage>>();
            localSocket.Setup(m => m.GetIdentity()).Returns(ReceiverIdentities.CreateForActor);
            var routes = new[] {localSocket.Object};
            internalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<InternalRouteLookupRequest>())).Returns(routes);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var otherNode = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            message.SetReceiverNode(otherNode);
            var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1:9009", otherNode.Identity)};
            externalRoutingTable.Setup(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.ReceiverNodeIdentity == otherNode))).Returns(new[] {peerConnection});
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            internalRoutingTable.Verify(m => m.FindRoutes(It.Is<InternalRouteLookupRequest>(req => req.Message.Equals(message))), Times.Never);
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message) && req.ReceiverNodeIdentity == otherNode)), Times.Once);
            scaleOutSocket.Verify(m => m.Connect(It.Is<string>(uri => uri == peerConnection.Node.Uri), true), Times.Once);
            scaleOutSocket.Verify(m => m.Send(message));
        }

        [Test]
        public void IfScaleOutSocketWasNotConnectedToRemoteNode_ConnectionIsEstablished()
        {
            messageRouter = CreateMessageRouter(null, externalRoutingTable.Object);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var otherNodeIdentifier = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var otherNode = new Node("tcp://127.0.0.1:9009", otherNodeIdentifier.Identity);
            var peerConnection = new PeerConnection
                                 {
                                     Node = otherNode,
                                     Health = new Health {Uri = "tcp://127.0.0.1:6767"}
                                 };
            message.SetReceiverNode(otherNodeIdentifier);

            externalRoutingTable.Setup(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.ReceiverNodeIdentity == otherNodeIdentifier))).Returns(new[] {peerConnection});
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            externalRoutingTable.Verify(m => m.FindRoutes(It.Is<ExternalRouteLookupRequest>(req => req.Message.Equals(message) && req.ReceiverNodeIdentity == otherNodeIdentifier)), Times.Once);
            scaleOutSocket.Verify(m => m.Connect(It.Is<string>(uri => uri == peerConnection.Node.Uri), true), Times.Once);
            clusterHealthMonitor.Verify(m => m.StartPeerMonitoring(It.Is<Node>(node => node.Equals(otherNode)),
                                                                   It.Is<Health>(health => health == peerConnection.Health)),
                                        Times.Once);
            Assert.True(peerConnection.Connected);
        }

        [Test]
        public void IfScaleOutBackendSocketSendMessageThrowsTimeoutException_ConnectivityCheckIsScheduled()
        {
            messageRouter = CreateMessageRouter(null, externalRoutingTable.Object);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var otherNode = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            message.SetReceiverNode(otherNode);
            var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1:9009", otherNode.Identity)};
            externalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<ExternalRouteLookupRequest>())).Returns(new[] {peerConnection});
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            scaleOutSocket.Setup(m => m.Send(It.IsAny<IMessage>())).Throws<TimeoutException>();
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            clusterHealthMonitor.Verify(m => m.ScheduleConnectivityCheck(It.Is<ReceiverIdentifier>(id => Unsafe.ArraysEqual(id.Identity, peerConnection.Node.SocketIdentity))), Times.Once);
        }

        [Test]
        public void IfScaleOutBackendSocketSendMessageThrowsHostUnreachableException_UnreachableNodeIsUnregistered()
        {
            messageRouter = CreateMessageRouter(null, externalRoutingTable.Object);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var otherNode = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            message.SetReceiverNode(otherNode);
            var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1:9009", otherNode.Identity)};
            externalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<ExternalRouteLookupRequest>())).Returns(new[] {peerConnection});
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            scaleOutSocket.Setup(m => m.Send(It.IsAny<IMessage>())).Throws<HostUnreachableException>();
            var unregPayload = new UnregisterUnreachableNodeMessage {ReceiverNodeIdentity = peerConnection.Node.SocketIdentity};
            var unregMessage = Message.Create(unregPayload);
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandlerRegistry.Setup(m => m.GetMessageHandler(It.Is<MessageIdentifier>(msg => msg.Equals(unregMessage)))).Returns(serviceMessageHandler.Object);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            serviceMessageHandlerRegistry.Verify(m => m.GetMessageHandler(It.Is<MessageIdentifier>(msg => msg.Equals(unregMessage))), Times.Once);
            serviceMessageHandler.Verify(m => m.Handle(unregMessage, scaleOutSocket.Object));
        }

        [Test]
        public void IfMessageIsUnhandled_RequestToDiscoverUnhandledMessageRouteIsSent()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            scaleOutSocket.Setup(m => m.Send(It.IsAny<IMessage>())).Throws<HostUnreachableException>();
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            clusterMonitor.Verify(m => m.DiscoverMessageRoute(It.Is<MessageRoute>(mr => mr.Message.Equals(message))), Times.Once);
        }

        [Test]
        public void IfMessageIsNotHandledLocally_ItIsForwardedAway()
        {
            messageRouter = CreateMessageRouter(externalRoutingTable: externalRoutingTable.Object);
            var message = Message.Create(new SimpleMessage()).As<Message>();
            var otherNode = new ReceiverIdentifier(Guid.NewGuid().ToByteArray());
            var peerConnection = new PeerConnection {Node = new Node("tcp://127.0.0.1:9009", otherNode.Identity)};
            externalRoutingTable.Setup(m => m.FindRoutes(It.IsAny<ExternalRouteLookupRequest>())).Returns(new[] {peerConnection});
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            scaleOutSocket.Verify(m => m.Send(message));
        }

        [Test]
        public void IfMessageFromRemoteActorIsUnhandled_NodeUnregistersSelfFromHandlingThisMessage()
        {
            var message = Message.Create(new SimpleMessage()).As<Message>();
            message.AddHop();
            localRouterSocket.SetupMessageReceived(message, ReceiveMessageDelay);
            scaleOutSocket.Setup(m => m.Send(It.IsAny<IMessage>())).Throws<HostUnreachableException>();
            //
            messageRouter.Start();
            ReceiveMessageCompletionDelay.Sleep();
            messageRouter.Stop();
            //
            clusterMonitor.Verify(m => m.UnregisterSelf(It.Is<IEnumerable<MessageRoute>>(mr => mr.First().Message.Equals(message))), Times.Once);
        }

        private MessageRouter CreateMessageRouter(IInternalRoutingTable internalRoutingTable = null,
                                                  IExternalRoutingTable externalRoutingTable = null)
            => new MessageRouter(socketFactory.Object,
                                 localSocketFactory.Object,
                                 internalRoutingTable ?? new InternalRoutingTable(roundRobinDestinationList.Object),
                                 externalRoutingTable ?? new ExternalRoutingTable(roundRobinDestinationList.Object, logger.Object),
                                 scaleOutConfigurationProvider.Object,
                                 clusterServices.Object,
                                 serviceMessageHandlerRegistry.Object,
                                 perfCounterManager.Object,
                                 securityProvider.Object,
                                 internalRegistrationHandler.Object,
                                 roundRobinDestinationList.Object,
                                 logger.Object);
    }
}