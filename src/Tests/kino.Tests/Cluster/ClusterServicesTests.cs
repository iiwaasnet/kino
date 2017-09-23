using kino.Cluster;
using Moq;
using Xunit;

namespace kino.Tests.Cluster
{
    public class ClusterServicesTests
    {
        private readonly ClusterServices clusterServices;
        private readonly Mock<IClusterMonitor> clusterMonitor;
        private readonly Mock<IScaleOutListener> scaleOutListener;
        private readonly Mock<IHeartBeatSender> heartBeatSender;
        private readonly Mock<IClusterHealthMonitor> clusterHealthMonitor;

        public ClusterServicesTests()
        {
            clusterMonitor = new Mock<IClusterMonitor>();
            scaleOutListener = new Mock<IScaleOutListener>();
            heartBeatSender = new Mock<IHeartBeatSender>();
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            clusterServices = new ClusterServices(clusterMonitor.Object,
                                                  scaleOutListener.Object,
                                                  heartBeatSender.Object,
                                                  clusterHealthMonitor.Object);
        }

        [Fact]
        public void WhenClusterServicesStarts_ItStartsAllOtherServices()
        {
            clusterServices.StartClusterServices();
            //
            clusterMonitor.Verify(m => m.Start(), Times.Once);
            scaleOutListener.Verify(m => m.Start(), Times.Once);
            heartBeatSender.Verify(m => m.Start(), Times.Once);
            clusterHealthMonitor.Verify(m => m.Start(), Times.Once);
        }

        [Fact]
        public void WhenClusterServicesStops_ItStopsAllOtherServices()
        {
            clusterServices.StopClusterServices();
            //
            clusterMonitor.Verify(m => m.Stop(), Times.Once);
            scaleOutListener.Verify(m => m.Stop(), Times.Once);
            heartBeatSender.Verify(m => m.Stop(), Times.Once);
            clusterHealthMonitor.Verify(m => m.Stop(), Times.Once);
        }

        [Fact]
        public void GetClusterMonitor_ReturnsClusterMonitor()
        {
            Assert.Equal(clusterMonitor.Object, clusterServices.GetClusterMonitor());
        }

        [Fact]
        public void GetClusterHealthMonitor_ReturnsClusterHealthMonitor()
        {
            Assert.Equal(clusterHealthMonitor.Object, clusterServices.GetClusterHealthMonitor());
        }
    }
}