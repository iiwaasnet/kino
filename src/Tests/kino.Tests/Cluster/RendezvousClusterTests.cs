using System.Linq;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core.Framework;
using kino.Tests.Helpers;
using Moq;
using Xunit;

namespace kino.Tests.Cluster
{
    public class RendezvousClusterTests
    {
        private readonly Mock<IConfigurationStorage<RendezvousClusterConfiguration>> configurationStorage;
        private readonly RendezvousCluster rendezvousCluster;
        private readonly RendezvousClusterConfiguration cluster;

        public RendezvousClusterTests()
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

        [Fact]
        public void RotateRendezvousServers_ReturnsRendezvousEndpointInRoundRobin()
        {
            foreach (var rendezvousEndpoint in cluster.Cluster)
            {
                Assert.Equal(rendezvousEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
                rendezvousCluster.RotateRendezvousServers();
            }
        }

        [Fact]
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
                Assert.Equal(rendezvousEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
                rendezvousCluster.RotateRendezvousServers();
            }
            foreach (var rendezvousEndpoint in cluster.Cluster)
            {
                Assert.NotEqual(rendezvousEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
                rendezvousCluster.RotateRendezvousServers();
            }
        }

        [Fact]
        public void SetCurrentRendezvousServer_SetsProvidedEndpointAsGetCurrentRendezvousServer()
        {
            Assert.Equal(cluster.Cluster.First(), rendezvousCluster.GetCurrentRendezvousServer());
            //
            var newRendezvous = cluster.Cluster.Third();
            rendezvousCluster.SetCurrentRendezvousServer(newRendezvous);
            //
            Assert.Equal(newRendezvous, rendezvousCluster.GetCurrentRendezvousServer());
        }

        [Fact]
        public void IfNewRendezvousServerDoesntBelongToCluster_ItIsNotSetAsCurrentRendezvousServer()
        {
            var otherEndpoint = new RendezvousEndpoint("tcp://*:5555", "tcp://*:4444");
            Assert.DoesNotContain(otherEndpoint, cluster.Cluster);
            //
            rendezvousCluster.SetCurrentRendezvousServer(otherEndpoint);
            //
            Assert.NotEqual(otherEndpoint, rendezvousCluster.GetCurrentRendezvousServer());
        }
    }
}