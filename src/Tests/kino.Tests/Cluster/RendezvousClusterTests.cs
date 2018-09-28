using System.Linq;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
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
                          Cluster = Randomizer.Int32(3, 6)
                                              .Produce(i => new RendezvousEndpoint($"tcp://*:808{i}", $"tcp://*:909{i}"))
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
                                 Cluster = Randomizer.Int32(3, 6)
                                                     .Produce(i => new RendezvousEndpoint($"tcp://*:8{i}8".ParseAddress().ToSocketAddress(),
                                                                                          $"tcp://*:9{i}9".ParseAddress().ToSocketAddress()))
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
            var otherEndpoint = new RendezvousEndpoint("tcp://*:5555", "tcp://*:4444");
            CollectionAssert.DoesNotContain(cluster.Cluster, otherEndpoint);
            //
            rendezvousCluster.SetCurrentRendezvousServer(otherEndpoint);
            //
            Assert.AreNotEqual(otherEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
        }
    }
}