using Castle.DynamicProxy;
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
        private readonly IHeartBeatSender heartBeatSender;
        private readonly IClusterHealthMonitor clusterHealthMonitor;

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
                                   ILogger logger,
                                   IHeartBeatSenderConfigurationManager heartBeatSenderConfigurationManager,
                                   ILocalSocketFactory localSocketFactory,
                                   ClusterHealthMonitorConfiguration clusterHealthMonitorConfiguration)
        {
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IClusterMonitor>()
                                 : (IClusterMonitor) new ClusterMonitor(scaleOutConfigurationProvider,
                                                                        autoDiscoverySender,
                                                                        autoDiscoveryListener,
                                                                        heartBeatConfigurationProvider,
                                                                        routeDiscovery,
                                                                        securityProvider);
            scaleOutListener = membershipConfiguration.RunAsStandalone
                                   ? (IScaleOutListener)new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IScaleOutListener>()
                                   : (IScaleOutListener) new ScaleOutListener(socketFactory,
                                                                              routerLocalSocket,
                                                                              scaleOutConfigurationManager,
                                                                              securityProvider,
                                                                              performanceCounterManager,
                                                                              logger);
            heartBeatSender = membershipConfiguration.RunAsStandalone
                                  ? (IHeartBeatSender)new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IHeartBeatSender>()
                                  : (IHeartBeatSender) new HeartBeatSender(socketFactory,
                                                                           heartBeatSenderConfigurationManager,
                                                                           scaleOutConfigurationProvider,
                                                                           logger);
            clusterHealthMonitor = membershipConfiguration.RunAsStandalone
                                       ? (IClusterHealthMonitor)new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IClusterHealthMonitor>()
                                       : (IClusterHealthMonitor) new ClusterHealthMonitor(socketFactory,
                                                                                          localSocketFactory,
                                                                                          clusterHealthMonitorConfiguration,
                                                                                          routerLocalSocket,
                                                                                          logger);
        }

        public IScaleOutListener GetScaleOutListener()
            => scaleOutListener;

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;

        public IHeartBeatSender GetHeartBeatSender()
            => heartBeatSender;

        public IClusterHealthMonitor GetClusterHealthMonitor()
            => clusterHealthMonitor;
    }
}