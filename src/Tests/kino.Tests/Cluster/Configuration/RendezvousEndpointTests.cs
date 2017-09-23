using kino.Cluster.Configuration;
using NUnit.Framework;

namespace kino.Tests.Cluster.Configuration
{
    
    public class RendezvousEndpointTests
    {
        [Fact]
        public void TwoRendezvousEndpointsAreEqual_IfTheirBroadsastUriAndUnicastUriPropertiesAreEqual()
        {
            const string unicastUri = "tcp://*:8080";
            const string broadcastUri = "tcp://*:9090";
            var first = new RendezvousEndpoint(unicastUri, broadcastUri);
            var second = new RendezvousEndpoint(unicastUri, broadcastUri);
            //
            Assert.Equal(first, second);
            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object) second));
            Assert.True(first == second);
            Assert.False(first != second);
        }

        [Fact]
        public void TwoRendezvousEndpointsAreNotEqual_IfTheirBroadsastUriPropertiesAreNotEqual()
        {
            const string unicastUri = "tcp://*:8080";
            var first = new RendezvousEndpoint(unicastUri, "tcp://*:9090");
            var second = new RendezvousEndpoint(unicastUri, "tcp://*:9091");
            //
            Assert.AreNotEqual(first, second);
            Assert.False(first.Equals(second));
            Assert.False(first.Equals((object) second));
            Assert.False(first == second);
            Assert.True(first != second);
        }

        [Fact]
        public void TwoRendezvousEndpointsAreNotEqual_IfTheirUnicastUriPropertiesAreNotEqual()
        {
            const string broadcastUri = "tcp://*:9090";
            var first = new RendezvousEndpoint("tcp://*:8081", broadcastUri);
            var second = new RendezvousEndpoint("tcp://*:8082", broadcastUri);
            //
            Assert.AreNotEqual(first, second);
            Assert.False(first.Equals(second));
            Assert.False(first.Equals((object) second));
            Assert.False(first == second);
            Assert.True(first != second);
        }
    }
}