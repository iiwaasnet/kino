using kino.Cluster.Configuration;
using NUnit.Framework;

namespace kino.Tests.Cluster.Configuration
{
    [TestFixture]
    public class RendezvousEndpointTests
    {
        [Test]
        public void TwoRendezvousEndpointsAreEqual_IfTheirBroadsastUriAndUnicastUriPropertiesAreEqual()
        {
            const string unicastUri = "tcp://*:8080";
            const string broadcastUri = "tcp://*:9090";
            var first = new RendezvousEndpoint(unicastUri, broadcastUri);
            var second = new RendezvousEndpoint(unicastUri, broadcastUri);
            //
            Assert.AreEqual(first, second);
            Assert.IsTrue(first.Equals(second));
            Assert.IsTrue(first.Equals((object) second));
            Assert.IsTrue(first == second);
            Assert.IsFalse(first != second);
        }

        [Test]
        public void TwoRendezvousEndpointsAreNotEqual_IfTheirBroadsastUriPropertiesAreNotEqual()
        {
            const string unicastUri = "tcp://*:8080";
            var first = new RendezvousEndpoint(unicastUri, "tcp://*:9090");
            var second = new RendezvousEndpoint(unicastUri, "tcp://*:9091");
            //
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(first.Equals((object) second));
            Assert.IsFalse(first == second);
            Assert.IsTrue(first != second);
        }

        [Test]
        public void TwoRendezvousEndpointsAreNotEqual_IfTheirUnicastUriPropertiesAreNotEqual()
        {
            const string broadcastUri = "tcp://*:9090";
            var first = new RendezvousEndpoint("tcp://*:8081", broadcastUri);
            var second = new RendezvousEndpoint("tcp://*:8082", broadcastUri);
            //
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(first.Equals((object) second));
            Assert.IsFalse(first == second);
            Assert.IsTrue(first != second);
        }
    }
}