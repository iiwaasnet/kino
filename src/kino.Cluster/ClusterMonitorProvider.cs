using kino.Configuration;
using kino.Security;

namespace kino.Cluster
{
    public class ClusterMonitorProvider : IClusterMonitorProvider
    {
        private readonly IClusterMonitor clusterMonitor;

        public ClusterMonitorProvider(ClusterMembershipConfiguration membershipConfiguration,
                                      IRouterConfigurationProvider routerConfigurationProvider,
                                      IClusterMembership clusterMembership,
                                      IClusterMessageSender clusterMessageSender,
                                      IClusterMessageListener clusterMessageListener,
                                      IRouteDiscovery routeDiscovery,
                                      ISecurityProvider securityProvider)
        {
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) new LoopbackClusterMonitor()
                                 : new ClusterMonitor(routerConfigurationProvider,
                                                      clusterMembership,
                                                      clusterMessageSender,
                                                      clusterMessageListener,
                                                      routeDiscovery,
                                                      securityProvider);
        }

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;
    }
}