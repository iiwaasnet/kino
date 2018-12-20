using System;
using System.Linq;
using C5;
using kino.Cluster.Configuration;
using kino.Core.Framework;
using CoreLib = System.Collections.Generic;

namespace kino.Rendezvous.Configuration
{
    public class PartnerNetworksConfigurationProvider : IPartnerNetworksConfigurationProvider
    {
        private CoreLib.IDictionary<string, PartnerClusterConfiguration> partnerClusters;

        public PartnerNetworksConfigurationProvider(CoreLib.IEnumerable<PartnerNetworkConfiguration> partners)
            => partnerClusters = AddConfiguration(partners);

        //TODO: Declare Event in IPartnerNetworksConfigurationProvider to fire when configuration is updated
        public void Update(CoreLib.IEnumerable<PartnerNetworkConfiguration> partners)
            => partnerClusters = AddConfiguration(partners);

        private CoreLib.IDictionary<string, PartnerClusterConfiguration> AddConfiguration(CoreLib.IEnumerable<PartnerNetworkConfiguration> partners)
        {
            AssertNetworksAreUnique(partners);

            var tmp = new CoreLib.Dictionary<string, PartnerClusterConfiguration>();

            foreach (var networkConfiguration in partners)
            {
                if (!tmp.ContainsKey(networkConfiguration.NetworkId))
                {
                    tmp.Add(networkConfiguration.NetworkId, GetConfiguration(networkConfiguration));
                }
                else
                {
                    throw new DuplicatedKeyException(networkConfiguration.NetworkId);
                }
            }

            return tmp;
        }

        private PartnerClusterConfiguration GetConfiguration(PartnerNetworkConfiguration networkConfiguration)
        {
            var tmp = new HashedLinkedList<RendezvousEndpoint>();
            tmp.AddAll(networkConfiguration.Cluster.Select(ep => new RendezvousEndpoint("tcp://*:1", ep)));

            if (tmp.Count < networkConfiguration.Cluster.Count())
            {
                throw new DuplicatedKeyException($"Initial Partner Cluster {networkConfiguration.NetworkId} configuration contains duplicated endpoints!");
            }

            return new PartnerClusterConfiguration
                   {
                       Cluster = tmp,
                       HeartBeatSilenceBeforeRendezvousFailover = networkConfiguration.HeartBeatSilenceBeforeRendezvousFailover,
                       AllowedDomains = networkConfiguration.AllowedDomains,
                       NetworkId = networkConfiguration.NetworkId
                   };
        }

        private static void AssertNetworksAreUnique(CoreLib.IEnumerable<PartnerNetworkConfiguration> partners)
        {
            var distinctNetworks = partners.GroupBy(cfg => cfg.NetworkId);
            if (distinctNetworks.Count() != partners.Count())
            {
                throw new Exception($"Configuration for Network(s) with Id(s): [{string.Join(",", distinctNetworks.Select(n => n.Key))}] is duplicated!");
            }
        }

        public CoreLib.IEnumerable<PartnerClusterConfiguration> PartnerNetworks => partnerClusters.Values;
    }
}