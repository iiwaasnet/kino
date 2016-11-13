using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMessageRoutesRequestHandlerTests
    {
        private string domain;
        private Mock<ISecurityProvider> securityProvider;
        private ClusterMessageRoutesRequestHandler handler;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IClusterMonitorProvider> clusterMonitorProvider;
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<ISocket> socket;

        [SetUp]
        public void Setup()
        {
            socket = new Mock<ISocket>();
            domain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.DomainIsAllowed(It.Is<string>(d => d != domain))).Returns(false);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterMonitorProvider = new Mock<IClusterMonitorProvider>();
            clusterMonitorProvider.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            handler = new ClusterMessageRoutesRequestHandler(clusterMonitorProvider.Object,
                                                             internalRoutingTable.Object,
                                                             securityProvider.Object);
        }

        [Test]
        public void IfDomainIsNotAllowed_SelfRegistrationIsNotSent()
        {
            var domain = Guid.NewGuid().ToString();
            var message = Message.Create(new RequestClusterMessageRoutesMessage(), domain);
            //
            handler.Handle(message, socket.Object);
            //
            internalRoutingTable.Verify(m => m.GetMessageIdentifiers(), Times.Never);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), domain), Times.Never);
        }

        [Test]
        public void IfForRequestedDomainNoMessageHandlersRegistered_SelfRegistrationIsNotSent()
        {
            var otherDomain = Guid.NewGuid().ToString();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            internalRoutingTable.Setup(m => m.GetMessageIdentifiers()).Returns(new[] {messageIdentifier});
            securityProvider.Setup(m => m.GetDomain(messageIdentifier.Identity)).Returns(otherDomain);
            var message = Message.Create(new RequestClusterMessageRoutesMessage(), domain);
            //
            handler.Handle(message, socket.Object);
            //
            internalRoutingTable.Verify(m => m.GetMessageIdentifiers(), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), domain), Times.Never);
        }

        [Test]
        public void IfForRequestedDomainNoMessageHandlersRegistered_SelfRegistrationIsSentOnlyForMessageHubs()
        {
            var otherDomain = Guid.NewGuid().ToString();
            var messageHandler = MessageIdentifier.Create<SimpleMessage>();
            var messageHub = new MessageIdentifier(Guid.NewGuid().ToByteArray());
            internalRoutingTable.Setup(m => m.GetMessageIdentifiers()).Returns(new[] {messageHandler, messageHub});
            securityProvider.Setup(m => m.GetDomain(messageHandler.Identity)).Returns(otherDomain);
            var message = Message.Create(new RequestClusterMessageRoutesMessage(), domain);
            //
            handler.Handle(message, socket.Object);
            //
            internalRoutingTable.Verify(m => m.GetMessageIdentifiers(), Times.Once);
            clusterMonitor.Verify(m => m.RegisterSelf(It.Is<IEnumerable<MessageIdentifier>>(ids => ids.SequenceEqual(new[] {messageHub})),
                                                      domain),
                                  Times.Once);
        }
    }
}