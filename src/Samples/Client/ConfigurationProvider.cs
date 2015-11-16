using System;
using System.Collections.Generic;
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
            => appConfig.RendezvousServers;

        public RouterConfiguration GetRouterConfiguration()
            => new RouterConfiguration
               {
                   RouterAddress = new SocketEndpoint(new Uri(appConfig.RouterUri), SocketIdentifier.CreateIdentity()),
                   ScaleOutAddress = new SocketEndpoint(new Uri(appConfig.ScaleOutAddressUri), SocketIdentifier.CreateIdentity())
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