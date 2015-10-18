using System;
using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;

namespace Server
{
    public class ConfigurationProvider : IConfigurationProvider
    {
        private readonly ApplicationConfiguration appConfig;

        public ConfigurationProvider(ApplicationConfiguration appConfig)
        {
            this.appConfig = appConfig;
        }

        public IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration()
            => appConfig.RendezvousServers.Select(rs => new RendezvousEndpoint
                                                        {
                                                            MulticastUri = new Uri(rs.BroadcastUri),
                                                            UnicastUri = new Uri(rs.UnicastUri)
                                                        });

        public RouterConfiguration GetRouterConfiguration()
            => new RouterConfiguration
               {
                   RouterAddress = new SocketEndpoint(new Uri(appConfig.RouterUri), SocketIdentifier.CreateIdentity()),
                   ScaleOutAddress = new SocketEndpoint(new Uri(appConfig.ScaleOutAddressUri), SocketIdentifier.CreateIdentity())
               };

        public ClusterMembershipConfiguration GetClusterTimingConfiguration()
            => new ClusterMembershipConfiguration
               {
                   PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion,
                   PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover,
                   RunAsStandalone = appConfig.RunAsStandalone
               };
    }
}