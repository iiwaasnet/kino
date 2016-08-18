using System;
using System.Collections.Generic;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class InternalMessageRouteRegistrationHandlerTests
    {
        private InternalMessageRouteRegistrationHandler handler;
        private InternalRoutingTable internalRoutingTable;
        private Mock<ILogger> logger;
        private Mock<IClusterMonitorProvider> clusterMonitorProvider;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<ISocket> socket;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterMonitorProvider = new Mock<IClusterMonitorProvider>();
            clusterMonitorProvider.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            internalRoutingTable = new InternalRoutingTable();
            securityProvider = new Mock<ISecurityProvider>();
            socket = new Mock<ISocket>();
            handler = new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
                                                                  internalRoutingTable,
                                                                  securityProvider.Object,
                                                                  logger.Object);
        }

        [Test]
        public void IfOnlyLocalMessageContractsAreRegistered_RegisterSelfIsNotCalled()
        {
            var message = Message.Create(new RegisterInternalMessageRouteMessage
                                         {
                                             SocketIdentity = Guid.NewGuid().ToByteArray(),
                                             Partition = Guid.NewGuid().ToByteArray(),
                                             LocalMessageContracts = new[]
                                                                     {
                                                                         new MessageContract
                                                                         {
                                                                             Identity = Guid.NewGuid().ToByteArray(),
                                                                             Version = Guid.NewGuid().ToByteArray(),
                                                                             Partition = Guid.NewGuid().ToByteArray()
                                                                         },
                                                                         new MessageContract
                                                                         {
                                                                             Identity = Guid.NewGuid().ToByteArray(),
                                                                             Version = Guid.NewGuid().ToByteArray(),
                                                                             Partition = Guid.NewGuid().ToByteArray()
                                                                         }
                                                                     }
                                         });
            //
            handler.Handle(message, socket.Object);
            //
            clusterMonitor.Verify(m => m.RegisterSelf(It.IsAny<IEnumerable<MessageIdentifier>>(), It.IsAny<string>()), Times.Never);
        }
    }
}