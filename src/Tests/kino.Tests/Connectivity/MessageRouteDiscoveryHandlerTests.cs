using System;
using System.Collections.Generic;
using System.Security;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class MessageRouteDiscoveryHandlerTests
    {
        private MessageRouteDiscoveryHandler messageRouteDiscoveryHandler;
        private Mock<IClusterMonitorProvider> clusterMonitorProvider;
        private Mock<IInternalRoutingTable> internalRoutingTable;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<ILogger> logger;
        private string domain;

        [SetUp]
        public void Setup()
        {
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterMonitorProvider = new Mock<IClusterMonitorProvider>();
            clusterMonitorProvider.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            internalRoutingTable = new Mock<IInternalRoutingTable>();
            securityProvider = new Mock<ISecurityProvider>();
            logger = new Mock<ILogger>();
            domain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            messageRouteDiscoveryHandler = new MessageRouteDiscoveryHandler(clusterMonitorProvider.Object,
                                                                            internalRoutingTable.Object,
                                                                            securityProvider.Object,
                                                                            logger.Object);
        }

        [Test]
        public void IfDiscoverRequestIsForMessageHubAndDomainIsAllowed_RegistrationIsSent()
        {
            var messageHubIdentifier = new MessageIdentifier(Guid.NewGuid().ToByteArray());
            internalRoutingTable.Setup(m => m.CanRouteMessage(It.IsAny<MessageIdentifier>())).Returns(true);
            var discoveryMessage = Message.Create(new DiscoverMessageRouteMessage
                                                  {
                                                      MessageContract = new MessageContract
                                                                        {
                                                                            Identity = messageHubIdentifier.Identity,
                                                                            Partition = messageHubIdentifier.Partition,
                                                                            Version = messageHubIdentifier.Version
                                                                        }
                                                  },
                                                  domain);

            messageRouteDiscoveryHandler.Handle(discoveryMessage, null);

            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), domain), Times.Once);
        }

        [Test]
        public void DiscoverMessageRouteMessageFromNotAllowedDomain_IsNotProcessed()
        {
            var domain = Guid.NewGuid().ToString();
            var discoveryMessage = Message.Create(new DiscoverMessageRouteMessage(), domain);

            messageRouteDiscoveryHandler.Handle(discoveryMessage, null);

            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), domain), Times.Never);
        }
    }
}