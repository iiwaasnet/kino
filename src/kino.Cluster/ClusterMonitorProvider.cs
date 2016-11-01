using kino.Configuration;
using kino.Security;

namespace kino.Cluster
{
    public class ClusterMonitorProvider : IClusterMonitorProvider
    {
        private readonly IClusterMonitor clusterMonitor;

        public ClusterMonitorProvider(ClusterMembershipConfiguration membershipConfiguration,
                                      IRouterConfigurationProvider routerConfigurationProvider,
                                      IAutoDiscoverySender autoDiscoverySender,
                                      IAutoDiscoveryListener autoDiscoveryListener,
                                      IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider,
                                      IRouteDiscovery routeDiscovery,
                                      ISecurityProvider securityProvider)
        {
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) new LoopbackClusterMonitor()
                                 : new ClusterMonitor(routerConfigurationProvider,
                                                      autoDiscoverySender,
                                                      autoDiscoveryListener,
                                                      heartBeatConfigurationProvider,
                                                      routeDiscovery,
                                                      securityProvider);
        }

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;
    }
}