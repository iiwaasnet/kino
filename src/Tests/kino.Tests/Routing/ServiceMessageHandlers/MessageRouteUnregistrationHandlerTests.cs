using System;
using System.Linq;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MessageContract = kino.Messaging.Messages.MessageContract;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class MessageRouteUnregistrationHandlerTests
    {
        private readonly Mock<IClusterHealthMonitor> clusterHealthMonitor;
        private readonly Mock<IExternalRoutingTable> externalRoutingTable;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly Mock<ILogger> logger;
        private readonly string domain;
        private readonly Mock<ISocket> backEndSocket;
        private readonly MessageRouteUnregistrationHandler handler;

        public MessageRouteUnregistrationHandlerTests()
        {
            domain = Guid.NewGuid().ToString();
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            backEndSocket = new Mock<ISocket>();
            logger = new Mock<ILogger>();
            handler = new MessageRouteUnregistrationHandler(clusterHealthMonitor.Object,
                                                            externalRoutingTable.Object,
                                                            securityProvider.Object,
                                                            logger.Object);
        }

        [Fact]
        public void IfDomainIsNotAllowed_ExternalMessageRouteIsNotRemoved()
        {
            var message = Message.Create(new UnregisterMessageRouteMessage()).As<Message>();
            message.SetDomain(Guid.NewGuid().ToString());
            //
            handler.Handle(message, backEndSocket.Object);
            //
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<ExternalRouteRegistration>()), Times.Never);
        }

        [Fact]
        public void EveryMessageRouteInTheReceivedMessage_IsRemoved()
        {
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            var receiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var payload = CreateUnregisterMessageRoutePayload(receiverNodeIdentity);
            var message = Message.Create(payload).As<Message>();
            var callsCount = payload.Routes.SelectMany(r => r.MessageContracts).Count();
            message.SetDomain(domain);
            var peerRemoveResult = new PeerRemoveResult {ConnectionAction = PeerConnectionAction.KeepConnection};
            externalRoutingTable.Setup(m => m.RemoveMessageRoute(It.IsAny<ExternalRouteRemoval>())).Returns(peerRemoveResult);
            //
            handler.Handle(message, backEndSocket.Object);
            //
            Func<ExternalRouteRemoval, bool> isRouteToRemove = route =>
                                                               {
                                                                   Assert.True(Unsafe.ArraysEqual(receiverNodeIdentity, route.NodeIdentifier));
                                                                   Assert.True(payload.Routes
                                                                                      .SelectMany(r => r.MessageContracts)
                                                                                      .Select(mc => new MessageIdentifier(mc.Identity, mc.Version, mc.Partition))
                                                                                      .Any(m => m.Equals(route.Route.Message)));
                                                                   Assert.True(payload.Routes
                                                                                      .Select(mc => new ReceiverIdentifier(mc.ReceiverIdentity))
                                                                                      .Any(receiver => receiver == route.Route.Receiver));
                                                                   return true;
                                                               };
            externalRoutingTable.Verify(m => m.RemoveMessageRoute(It.Is<ExternalRouteRemoval>(rt => isRouteToRemove(rt))), Times.Exactly(callsCount));
        }

        [Fact]
        public void IfRouteReceiverIsMessageHub_MessageDominIsNotChecked()
        {
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(Guid.NewGuid().ToString);
            var receiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var payload = CreateUnregisterMessageRoutePayload(receiverNodeIdentity, ReceiverIdentities.CreateForMessageHub().Identity);
            var message = Message.Create(payload).As<Message>();
            var callsCount = payload.Routes.SelectMany(r => r.MessageContracts).Count();
            message.SetDomain(domain);
            var peerRemoveResult = new PeerRemoveResult {ConnectionAction = PeerConnectionAction.KeepConnection};
            externalRoutingTable.Setup(m => m.RemoveMessageRoute(It.IsAny<ExternalRouteRemoval>())).Returns(peerRemoveResult);
            //
            handler.Handle(message, backEndSocket.Object);
            //
            Func<ExternalRouteRemoval, bool> isRouteToRemove = route =>
                                                               {
                                                                   Assert.True(Unsafe.ArraysEqual(receiverNodeIdentity, route.NodeIdentifier));
                                                                   Assert.True(payload.Routes
                                                                                      .SelectMany(r => r.MessageContracts)
                                                                                      .Select(mc => new MessageIdentifier(mc.Identity, mc.Version, mc.Partition))
                                                                                      .Any(m => m.Equals(route.Route.Message)));
                                                                   Assert.True(payload.Routes
                                                                                      .Select(mc => new ReceiverIdentifier(mc.ReceiverIdentity))
                                                                                      .Any(receiver => receiver == route.Route.Receiver));
                                                                   return true;
                                                               };
            externalRoutingTable.Verify(m => m.RemoveMessageRoute(It.Is<ExternalRouteRemoval>(rt => isRouteToRemove(rt))), Times.Exactly(callsCount));
            securityProvider.Verify(m => m.GetDomain(It.IsAny<byte[]>()), Times.Never());
        }

        [Fact]
        public void IfRouteReceiverIsActorAndMessageDomainIsNotEqualToUnregisterMessageRouteDomain_ExternalMessageRouteIsNotRemoved()
        {
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(Guid.NewGuid().ToString);
            var receiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var payload = CreateUnregisterMessageRoutePayload(receiverNodeIdentity, ReceiverIdentities.CreateForActor().Identity);
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            var peerRemoveResult = new PeerRemoveResult {ConnectionAction = PeerConnectionAction.KeepConnection};
            externalRoutingTable.Setup(m => m.RemoveMessageRoute(It.IsAny<ExternalRouteRemoval>())).Returns(peerRemoveResult);
            var callsCount = payload.Routes.SelectMany(r => r.MessageContracts).Count();
            //
            handler.Handle(message, backEndSocket.Object);
            //
            externalRoutingTable.Verify(m => m.RemoveMessageRoute(It.IsAny<ExternalRouteRemoval>()), Times.Never);
            securityProvider.Verify(m => m.GetDomain(It.IsAny<byte[]>()), Times.Exactly(callsCount));
        }

        [Theory]
        [InlineData(PeerConnectionAction.Disconnect)]
        [InlineData(PeerConnectionAction.KeepConnection)]
        [InlineData(PeerConnectionAction.NotFound)]
        public void IfPeerRemovalConnectionActionIsDisconnect_ScaleOutBackendSocketIsDisconnectedFromPeer(PeerConnectionAction peerConnectionAction)
        {
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            var receiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var payload = CreateUnregisterMessageRoutePayload(receiverNodeIdentity);
            var message = Message.Create(payload).As<Message>();
            var callsCount = payload.Routes.SelectMany(r => r.MessageContracts).Count();
            message.SetDomain(domain);
            var peerRemoveResult = new PeerRemoveResult
                                   {
                                       ConnectionAction = peerConnectionAction,
                                       Uri = new Uri("tcp://127.0.0.1:9090")
                                   };
            externalRoutingTable.Setup(m => m.RemoveMessageRoute(It.IsAny<ExternalRouteRemoval>())).Returns(peerRemoveResult);
            //
            handler.Handle(message, backEndSocket.Object);
            //
            backEndSocket.Verify(m => m.Disconnect(peerRemoveResult.Uri), Times.Exactly(peerConnectionAction == PeerConnectionAction.Disconnect ? callsCount : 0));
        }

        [Theory]
        [InlineData(PeerConnectionAction.Disconnect)]
        [InlineData(PeerConnectionAction.KeepConnection)]
        [InlineData(PeerConnectionAction.NotFound)]
        public void IfPeerRemovalConnectionActionNotEqualsKeepConnection_ClusterHealthMonitorDeletesPeer(PeerConnectionAction peerConnectionAction)
        {
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            var receiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var payload = CreateUnregisterMessageRoutePayload(receiverNodeIdentity);
            var message = Message.Create(payload).As<Message>();
            var callsCount = payload.Routes.SelectMany(r => r.MessageContracts).Count();
            message.SetDomain(domain);
            var peerRemoveResult = new PeerRemoveResult
                                   {
                                       ConnectionAction = peerConnectionAction,
                                       Uri = new Uri("tcp://127.0.0.1:9090")
                                   };
            externalRoutingTable.Setup(m => m.RemoveMessageRoute(It.IsAny<ExternalRouteRemoval>())).Returns(peerRemoveResult);
            //
            handler.Handle(message, backEndSocket.Object);
            //
            clusterHealthMonitor.Verify(m => m.DeletePeer(new ReceiverIdentifier(payload.ReceiverNodeIdentity)),
                                        Times.Exactly(peerConnectionAction != PeerConnectionAction.KeepConnection ? callsCount : 0));
        }

        private static UnregisterMessageRouteMessage CreateUnregisterMessageRoutePayload(byte[] receiverNodeIdentity, byte[] receiverIdentity = null)
        {
            var payload = new UnregisterMessageRouteMessage
                          {
                              Routes = Randomizer.Int32(2, 5)
                                                 .Produce(() => new RouteRegistration
                                                                {
                                                                    MessageContracts = EnumerableExtensions
                                                                        .Produce(Randomizer.Int32(2, 5),
                                                                                 () => new MessageContract
                                                                                       {
                                                                                           Identity = Guid.NewGuid().ToByteArray(),
                                                                                           Version = Randomizer.UInt16(),
                                                                                           Partition = Guid.NewGuid().ToByteArray()
                                                                                       })
                                                                        .ToArray(),
                                                                    ReceiverIdentity = receiverIdentity ?? Guid.NewGuid().ToByteArray()
                                                                })
                                                 .ToArray(),
                              ReceiverNodeIdentity = receiverNodeIdentity
                          };
            return payload;
        }
    }
}