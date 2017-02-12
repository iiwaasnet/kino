using System.Linq;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    [TestFixture]
    public class RendezvousClusterTests
    {
        private Mock<IConfigurationStorage<RendezvousClusterConfiguration>> configurationStorage;
        private RendezvousCluster rendezvousCluster;
        private RendezvousClusterConfiguration cluster;

        [SetUp]
        public void Setup()
        {
            configurationStorage = new Mock<IConfigurationStorage<RendezvousClusterConfiguration>>();
            cluster = new RendezvousClusterConfiguration
                      {
                          Cluster = EnumerableExtensions.Produce(Randomizer.Int32(3, 6),
                                                                i => new RendezvousEndpoint($"tcp://127.0.0.{i}:8080", $"tcp://127.0.0.{i}:9090"))
                      };
            configurationStorage.Setup(m => m.Read()).Returns(cluster);
            rendezvousCluster = new RendezvousCluster(configurationStorage.Object);
        }

        [Test]
        public void RotateRendezvousServers_ReturnsRendezvousEndpointInRoundRobin()
        {
            foreach (var rendezvousEndpoint in cluster.Cluster)
            {
                Assert.AreEqual(rendezvousEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
                rendezvousCluster.RotateRendezvousServers();
            }
        }

        [Test]
        public void IfRendezvousClusterReconfigured_OldEndpointsRemovedAndNewAdded()
        {
            var newCluster = new RendezvousClusterConfiguration
                             {
                                 Cluster = EnumerableExtensions.Produce(Randomizer.Int32(3, 6),
                                                                       i => new RendezvousEndpoint($"tcp://127.0.2.{i}:8081", $"tcp://127.0.3.{i}:9092"))
                             };
            configurationStorage.Setup(m => m.Read()).Returns(newCluster);
            //
            rendezvousCluster.Reconfigure(newCluster.Cluster);
            //
            foreach (var rendezvousEndpoint in newCluster.Cluster)
            {
                Assert.AreEqual(rendezvousEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
                rendezvousCluster.RotateRendezvousServers();
            }
            foreach (var rendezvousEndpoint in cluster.Cluster)
            {
                Assert.AreNotEqual(rendezvousEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
                rendezvousCluster.RotateRendezvousServers();
            }
        }

        [Test]
        public void SetCurrentRendezvousServer_SetsProvidedEndpointAsGetCurrentRendezvousServer()
        {
            Assert.AreEqual(cluster.Cluster.First(), rendezvousCluster.GetCurrentRendezvousServer());
            //
            var newRendezvous = cluster.Cluster.Third();
            rendezvousCluster.SetCurrentRendezvousServer(newRendezvous);
            //
            Assert.AreEqual(newRendezvous, rendezvousCluster.GetCurrentRendezvousServer());
        }

        [Test]
        public void IfNewRendezvousServerDoesntBelongToCluster_ItIsNotSetAsCurrentRendezvousServer()
        {
            var otherEndpoint = new RendezvousEndpoint("tcp://192.168.0.1:5555", "tcp://192.168.0.12:4444");
            CollectionAssert.DoesNotContain(cluster.Cluster, otherEndpoint);
            //
            rendezvousCluster.SetCurrentRendezvousServer(otherEndpoint);
            //
            Assert.AreNotEqual(otherEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
        }
    }
}