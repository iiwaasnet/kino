using kino.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Security;

namespace kino.Cluster
{
    public class ClusterConnectivity : IClusterConnectivity
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IScaleOutListener scaleOutListener;

        public ClusterConnectivity(ClusterMembershipConfiguration membershipConfiguration,
                                   IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                                   IAutoDiscoverySender autoDiscoverySender,
                                   IAutoDiscoveryListener autoDiscoveryListener,
                                   IHeartBeatSenderConfigurationProvider heartBeatConfigurationProvider,
                                   IRouteDiscovery routeDiscovery,
                                   ISocketFactory socketFactory,
                                   ILocalSendingSocket<IMessage> routerLocalSocket,
                                   IScaleOutConfigurationManager scaleOutConfigurationManager,
                                   ISecurityProvider securityProvider,
                                   IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                   ILogger logger)
        {
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) new LoopbackClusterMonitor()
                                 : (IClusterMonitor) new ClusterMonitor(scaleOutConfigurationProvider,
                                                                        autoDiscoverySender,
                                                                        autoDiscoveryListener,
                                                                        heartBeatConfigurationProvider,
                                                                        routeDiscovery,
                                                                        securityProvider);
            scaleOutListener = membershipConfiguration.RunAsStandalone
                                   ? (IScaleOutListener) new NullScaleOutListener()
                                   : (IScaleOutListener) new ScaleOutListener(socketFactory,
                                                                              routerLocalSocket,
                                                                              scaleOutConfigurationManager,
                                                                              securityProvider,
                                                                              performanceCounterManager,
                                                                              logger);
        }

        public IScaleOutListener GetScaleOutListener()
            => scaleOutListener;

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;
    }
}