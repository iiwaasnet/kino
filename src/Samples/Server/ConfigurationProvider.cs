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

        public IEnumerable<RendezvousEndpoints> GetRendezvousEndpointsConfiguration()
            => appConfig.RendezvousServers.Select(rs => new RendezvousEndpoints
                                                        {
                                                            MulticastUri = new Uri(rs.BroadcastUri),
                                                            UnicastUri = new Uri(rs.UnicastUri)
                                                        });

        public RouterConfiguration GetRouterConfiguration()
            => new RouterConfiguration
               {
                   RouterAddress = new SocketEndpoint(new Uri(appConfig.RouterUri), SocketIdentifier.CreateNew()),
                   ScaleOutAddress = new SocketEndpoint(new Uri(appConfig.ScaleOutAddressUri), SocketIdentifier.CreateNew())
               };

        public ClusterTimingConfiguration GetClusterTimingConfiguration()
            => new ClusterTimingConfiguration
               {
                   PongSilenceBeforeRouteDeletion = appConfig.PongSilenceBeforeRouteDeletion,
                   PingSilenceBeforeRendezvousFailover = appConfig.PingSilenceBeforeRendezvousFailover
               };
    }
}