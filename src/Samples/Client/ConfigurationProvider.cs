using System;
using System.Collections.Generic;
using System.Linq;
using kino.Client;
using kino.Core.Connectivity;

namespace Client
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

        public RouterConfiguration GetRouterConfiguration()
            => new RouterConfiguration
               {
                   RouterAddress = new SocketEndpoint(appConfig.RouterUri),
                   ScaleOutAddress = new SocketEndpoint(appConfig.ScaleOutAddressUri),
                   DeferPeerConnection = appConfig.DeferPeerConnection
               };

        public ClusterMembershipConfiguration GetClusterMembershipConfiguration()
            => new ClusterMembershipConfiguration
               {
                   PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion,
                   PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover,
                   RunAsStandalone = appConfig.RunAsStandalone
               };

        public MessageHubConfiguration GetMessageHubConfiguration()
            => new MessageHubConfiguration {RouterUri = new Uri(appConfig.RouterUri)};
    }
}