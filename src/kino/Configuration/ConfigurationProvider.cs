using System;
using System.Collections.Generic;
using System.Linq;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;

namespace kino.Configuration
{
    public class ConfigurationProvider : IConfigurationProvider
    {
        private readonly ApplicationConfiguration appConfig;

        public ConfigurationProvider(ApplicationConfiguration appConfig)
        {
            this.appConfig = appConfig;
        }

        public IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration()
            => appConfig.RendezvousServers.Select(s => new RendezvousEndpoint(s.UnicastUri, s.BroadcastUri));

        //TODO: Remove
        public RouterConfiguration GetRouterConfiguration()
            => new RouterConfiguration();

        public ScaleOutSocketConfiguration GetScaleOutConfiguration()
        {
            var uris = appConfig.ScaleOutAddressUri.GetAddressRange();
            var socketIdentifier = SocketIdentifier.CreateIdentity();

            return new ScaleOutSocketConfiguration
                   {
                       AddressRange = uris.Select(uri => new SocketEndpoint(uri, socketIdentifier))
                                          .ToList()
                   };
        }

        public ClusterMembershipConfiguration GetClusterMembershipConfiguration()
            => new ClusterMembershipConfiguration
               {
                   HeartBeatSilenceBeforeRendezvousFailover = appConfig.HeartBeatSilenceBeforeRendezvousFailover,
                   RunAsStandalone = appConfig.RunAsStandalone
               };

        public ClusterHealthMonitorConfiguration GetClusterHealthMonitorConfiguration()
            => new ClusterHealthMonitorConfiguration
               {
                   IntercomEndpoint = new Uri(appConfig.Health.IntercomEndpoint),
                   MissingHeartBeatsBeforeDeletion = appConfig.Health.MissingHeartBeatsBeforeDeletion,
                   PeerIsStaleAfter = appConfig.Health.PeerIsStaleAfter,
                   StalePeersCheckInterval = appConfig.Health.StalePeersCheckInterval
               };

        public HeartBeatSenderConfiguration GetHeartBeatSenderConfiguration()
            => new HeartBeatSenderConfiguration
               {
                   HeartBeatInterval = appConfig.Health.HeartBeatInterval,
                   AddressRange = appConfig.Health.HeartBeatUri.GetAddressRange().ToList()
               };
    }
}