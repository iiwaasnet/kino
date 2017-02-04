using System;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    [TestFixture]
    public class NodeUnregistrationHandlerTests
    {
        private NodeUnregistrationHandler handler;
        private Mock<IClusterHealthMonitor> clusterHealthMonitor;
        private Mock<IExternalRoutingTable> externalRoutingTable;
        private Mock<ISecurityProvider> securityProvider;
        private string domain;
        private Mock<ISocket> backendSocket;

        [SetUp]
        public void Setup()
        {
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            backendSocket = new Mock<ISocket>();
            handler = new NodeUnregistrationHandler(clusterHealthMonitor.Object,
                                                    externalRoutingTable.Object,
                                                    securityProvider.Object);
        }

        [Test]
        public void IfDomainIsNotAllowed_NodeRouteIsNotRemoved()
        {
            var message = Message.Create(new UnregisterNodeMessage()).As<Message>();
            message.SetDomain(Guid.NewGuid().ToString());
            //
            handler.Handle(message, null);
            //
            externalRoutingTable.Verify(m => m.RemoveNodeRoute(It.IsAny<ReceiverIdentifier>()), Times.Never);
            backendSocket.Verify(m => m.Disconnect(It.IsAny<Uri>()), Times.Never);
            clusterHealthMonitor.Verify(m => m.DeletePeer(It.IsAny<ReceiverIdentifier>()), Times.Never);
        }

        [Test(Description = "ScaleOutBackend socket disconnects peer only if PeerConnectionAction is Disconnect. " +
                            "ClusterHealthMonitor deletes peer if PeerConnectionAction is not KeepConnection.")]
        [TestCase(PeerConnectionAction.Disconnect)]
        [TestCase(PeerConnectionAction.KeepConnection)]
        [TestCase(PeerConnectionAction.KeepConnection)]
        public void BackendSocketDisconnectsPeer_OnlyIfPeerConnectionActionIsDisconnect(PeerConnectionAction peerConnectionAction)
        {
            var payload = new UnregisterNodeMessage {ReceiverNodeIdentity = Guid.NewGuid().ToByteArray()};
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            var peerRemoveResult = new PeerRemoveResult
                                   {
                                       ConnectionAction = peerConnectionAction,
                                       Uri = new Uri("tcp://127.0.0.1:4556")
                                   };
            var receiverIdentifier = new ReceiverIdentifier(payload.ReceiverNodeIdentity);
            externalRoutingTable.Setup(m => m.RemoveNodeRoute(receiverIdentifier))
                                .Returns(peerRemoveResult);
            //
            handler.Handle(message, backendSocket.Object);
            //
            externalRoutingTable.Verify(m => m.RemoveNodeRoute(receiverIdentifier), Times.Once);
            backendSocket.Verify(m => m.Disconnect(peerRemoveResult.Uri), Times.Exactly(peerConnectionAction == PeerConnectionAction.Disconnect ? 1 : 0));
            clusterHealthMonitor.Verify(m => m.DeletePeer(receiverIdentifier), Times.Exactly(peerConnectionAction != PeerConnectionAction.KeepConnection ? 1 : 0));
        }
    }
}