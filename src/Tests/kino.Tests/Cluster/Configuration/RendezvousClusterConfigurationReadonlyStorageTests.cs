using System.Collections.Generic;
using kino.Cluster.Configuration;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Cluster.Configuration
{
    [TestFixture]
    public class RendezvousClusterConfigurationReadonlyStorageTests
    {
        private RendezvousClusterConfigurationReadonlyStorage configStorage;
        private IEnumerable<RendezvousEndpoint> initialConfiguration;

        [SetUp]
        public void Setup()
        {
            initialConfiguration = EnumerableExtenions.Produce(Randomizer.Int32(3, 6),
                                                               i => new RendezvousEndpoint($"tcp://127.0.0.{i}:8080", $"tcp://127.0.0.{i}:9090"));
            configStorage = new RendezvousClusterConfigurationReadonlyStorage(initialConfiguration);
        }

        [Test]
        public void Update_RemovesAllPreviousRendezvousEndpointsAndAddsNewOnes()
        {
            var config = new RendezvousClusterConfiguration
                         {
                             Cluster = EnumerableExtenions.Produce(Randomizer.Int32(3, 6),
                                                                   i => new RendezvousEndpoint($"tcp://192.0.0.{i}:8080", $"tcp://192.0.0.{i}:9090"))
                         };
            CollectionAssert.AreEquivalent(initialConfiguration, configStorage.Read().Cluster);
            //
            configStorage.Update(config);
            //
            CollectionAssert.AreEquivalent(config.Cluster, configStorage.Read().Cluster);
        }
    }
}