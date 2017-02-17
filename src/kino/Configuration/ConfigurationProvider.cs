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
        private readonly KinoConfiguration appConfig;

        public ConfigurationProvider(KinoConfiguration appConfig)
        {
            this.appConfig = appConfig;
        }

        public IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration()
            => appConfig.RendezvousServers.Select(s => new RendezvousEndpoint(s.UnicastUri, s.BroadcastUri));

        public ScaleOutSocketConfiguration GetScaleOutConfiguration()
        {
            var uris = appConfig.ScaleOutAddressUri.GetAddressRange();
            var socketIdentifier = ReceiverIdentifier.CreateIdentity();

            return new ScaleOutSocketConfiguration
                   {
                       ScaleOutReceiveMessageQueueLength = Math.Max(appConfig.ScaleOutReceiveMessageQueueLength, 10000),
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