using System.Collections.Generic;
using FluentAssertions;
using kino.Cluster.Configuration;
using kino.Tests.Helpers;
using Xunit;

namespace kino.Tests.Cluster.Configuration
{
    public class RendezvousClusterConfigurationReadonlyStorageTests
    {
        private readonly RendezvousClusterConfigurationReadonlyStorage configStorage;
        private readonly IEnumerable<RendezvousEndpoint> initialConfiguration;

        public RendezvousClusterConfigurationReadonlyStorageTests()
        {
            initialConfiguration = Randomizer.Int32(3, 6)
                                             .Produce(i => new RendezvousEndpoint($"tcp://*:808{i}", $"tcp://*:909{i}"));
            configStorage = new RendezvousClusterConfigurationReadonlyStorage(initialConfiguration);
        }

        [Fact]
        public void Update_RemovesAllPreviousRendezvousEndpointsAndAddsNewOnes()
        {
            var config = new RendezvousClusterConfiguration
                         {
                             Cluster = Randomizer.Int32(3, 6)
                                                 .Produce(i => new RendezvousEndpoint($"tcp://*:8{i}80", $"tcp://*:9{i}90"))
                         };
            initialConfiguration.Should().BeEquivalentTo(configStorage.Read().Cluster);
            //
            configStorage.Update(config);
            //
            config.Cluster.Should().BeEquivalentTo(configStorage.Read().Cluster);
        }
    }
}