using System;
using System.Linq;
using C5;
using kino.Cluster.Configuration;
using kino.Core.Framework;

namespace kino.Rendezvous.Configuration
{
    public class PartnerNetworksConfigurationReadonlyStorage : IConfigurationStorage<RendezvousClusterConfiguration>,
                                                               IConfigurationStorage<PartnerClusterConfiguration>
    {
        private volatile PartnerClusterConfiguration config;

        public PartnerNetworksConfigurationReadonlyStorage(PartnerNetworkConfiguration initialConfiguration)
            => config = NormalizeConfiguration(initialConfiguration);

        public RendezvousClusterConfiguration Read()
            => new RendezvousClusterConfiguration {Cluster = config.Cluster};

        public void Update(RendezvousClusterConfiguration newConfig)
            => throw new NotImplementedException();

        PartnerClusterConfiguration IConfigurationStorage<PartnerClusterConfiguration>.Read()
            => config;

        public void Update(PartnerClusterConfiguration newConfig)
        {
            if (config.NetworkId != newConfig.NetworkId)
            {
                throw new Exception($"{nameof(newConfig.NetworkId)} mismatch for new configuration! "
                                  + $"(new:{newConfig.NetworkId}!= current:{config.NetworkId})");
            }
            config = NormalizeConfiguration(new PartnerNetworkConfiguration
                                            {
                                                AllowedDomains = newConfig.AllowedDomains,
                                                NetworkId = newConfig.NetworkId,
                                                Cluster = newConfig.Cluster.Select(node => node.BroadcastUri)
                                            });
        }

        private static PartnerClusterConfiguration NormalizeConfiguration(PartnerNetworkConfiguration initialConfiguration)
        {
            var tmp = new HashedLinkedList<RendezvousEndpoint>();
            tmp.AddAll(initialConfiguration.Cluster.Select(ep => new RendezvousEndpoint("tcp://*:1", ep)));

            if (tmp.Count < initialConfiguration.Cluster.Count())
            {
                throw new DuplicatedKeyException("Initial Partner Cluster configuration contains duplicated endpoints!");
            }

            return new PartnerClusterConfiguration
                   {
                       Cluster = tmp,
                       AllowedDomains = initialConfiguration.AllowedDomains,
                       NetworkId = initialConfiguration.NetworkId
                   };
        }
    }
}