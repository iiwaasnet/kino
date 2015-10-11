using System;
using System.Collections.Generic;
using System.Linq;
using kino.Client;
using kino.Connectivity;
using kino.Framework;

namespace Client
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

        public ExpirableItemCollectionConfiguration GetExpirableItemCollectionConfiguration()
            => new ExpirableItemCollectionConfiguration {EvaluationInterval = appConfig.PromiseExpirationEvaluationInterval};

        public MessageHubConfiguration GetMessageHubConfiguration()
            => new MessageHubConfiguration {RouterUri = new Uri(appConfig.RouterUri)};
    }
}