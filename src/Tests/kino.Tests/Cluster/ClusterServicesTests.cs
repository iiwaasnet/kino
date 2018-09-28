using kino.Cluster;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    public class ClusterServicesTests
    {
        private ClusterServices clusterServices;
        private Mock<IClusterMonitor> clusterMonitor;
        private Mock<IScaleOutListener> scaleOutListener;
        private Mock<IHeartBeatSender> heartBeatSender;
        private Mock<IClusterHealthMonitor> clusterHealthMonitor;

        [SetUp]
        public void Setup()
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

        [Test]
        public void WhenClusterServicesStarts_ItStartsAllOtherServices()
        {
            clusterServices.StartClusterServices();
            //
            clusterMonitor.Verify(m => m.Start(), Times.Once);
            scaleOutListener.Verify(m => m.Start(), Times.Once);
            heartBeatSender.Verify(m => m.Start(), Times.Once);
            clusterHealthMonitor.Verify(m => m.Start(), Times.Once);
        }

        [Test]
        public void WhenClusterServicesStops_ItStopsAllOtherServices()
        {
            clusterServices.StopClusterServices();
            //
            clusterMonitor.Verify(m => m.Stop(), Times.Once);
            scaleOutListener.Verify(m => m.Stop(), Times.Once);
            heartBeatSender.Verify(m => m.Stop(), Times.Once);
            clusterHealthMonitor.Verify(m => m.Stop(), Times.Once);
        }

        [Test]
        public void GetClusterMonitor_ReturnsClusterMonitor()
        {
            Assert.AreEqual(clusterMonitor.Object, clusterServices.GetClusterMonitor());
        }

        [Test]
        public void GetClusterHealthMonitor_ReturnsClusterHealthMonitor()
        {
            Assert.AreEqual(clusterHealthMonitor.Object, clusterServices.GetClusterHealthMonitor());
        }
    }
}