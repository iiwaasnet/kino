using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Security;
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

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            clusterMonitor = new Mock<IClusterMonitor>();
            clusterMonitorProvider = new Mock<IClusterMonitorProvider>();
            clusterMonitorProvider.Setup(m => m.GetClusterMonitor()).Returns(clusterMonitor.Object);
            internalRoutingTable = new InternalRoutingTable();
            securityProvider = new Mock<ISecurityProvider>();
            handler = new InternalMessageRouteRegistrationHandler(clusterMonitorProvider.Object,
                                                                  internalRoutingTable,
                                                                  securityProvider.Object,
                                                                  logger.Object);
        }
    }
}