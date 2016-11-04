using System.Collections.Generic;
using Castle.DynamicProxy;
using kino.Configuration;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
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
                                   IRouteDiscovery routeDiscovery,
                                   ISocketFactory socketFactory,
                                   ILocalSocket<IMessage> routerLocalSocket,
                                   IScaleOutConfigurationManager scaleOutConfigurationManager,
                                   ISecurityProvider securityProvider,
                                   IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                   ILogger logger,
                                   IHeartBeatSenderConfigurationManager heartBeatSenderConfigurationManager,
                                   ILocalSocketFactory localSocketFactory,
                                   ClusterHealthMonitorConfiguration clusterHealthMonitorConfiguration)
        {
            var proxyGenerator = new ProxyGenerator();
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) proxyGenerator.CreateInterfaceProxyWithoutTarget<IClusterMonitor>()
                                 : (IClusterMonitor) new ClusterMonitor(scaleOutConfigurationProvider,
                                                                        autoDiscoverySender,
                                                                        autoDiscoveryListener,
                                                                        heartBeatSenderConfigurationManager.As<IHeartBeatSenderConfigurationProvider>(),
                                                                        routeDiscovery,
                                                                        securityProvider);
            scaleOutListener = membershipConfiguration.RunAsStandalone
                                   ? (IScaleOutListener) proxyGenerator.CreateInterfaceProxyWithoutTarget<IScaleOutListener>()
                                   : (IScaleOutListener) new ScaleOutListener(socketFactory,
                                                                              routerLocalSocket,
                                                                              scaleOutConfigurationManager,
                                                                              securityProvider,
                                                                              performanceCounterManager,
                                                                              logger);
            heartBeatSender = membershipConfiguration.RunAsStandalone
                                  ? (IHeartBeatSender) proxyGenerator.CreateInterfaceProxyWithoutTarget<IHeartBeatSender>()
                                  : (IHeartBeatSender) new HeartBeatSender(socketFactory,
                                                                           heartBeatSenderConfigurationManager,
                                                                           scaleOutConfigurationProvider,
                                                                           logger);
            clusterHealthMonitor = membershipConfiguration.RunAsStandalone
                                       ? (IClusterHealthMonitor) proxyGenerator.CreateInterfaceProxyWithoutTarget<IClusterHealthMonitor>()
                                       : (IClusterHealthMonitor) new ClusterHealthMonitor(socketFactory,
                                                                                          localSocketFactory,
                                                                                          clusterHealthMonitorConfiguration,
                                                                                          routerLocalSocket,
                                                                                          logger);
        }

        public void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain)
            => clusterMonitor.RegisterSelf(messageHandlers, domain);

        public void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers)
            => clusterMonitor.UnregisterSelf(messageIdentifiers);

        public void DiscoverMessageRoute(Identifier messageIdentifier)
            => clusterMonitor.DiscoverMessageRoute(messageIdentifier);

        public void StartPeerMonitoring(SocketIdentifier socketIdentifier, Health health)
            => clusterHealthMonitor.StartPeerMonitoring(socketIdentifier, health);

        public void DeletePeer(SocketIdentifier socketIdentifier)
            => clusterHealthMonitor.DeletePeer(socketIdentifier);

        public void StartClusterServices()
        {
            clusterMonitor.Start();
            scaleOutListener.Start();
            heartBeatSender.Start();
            clusterHealthMonitor.Start();
        }

        public void StopClusterServices()
        {
            clusterMonitor.Stop();
            scaleOutListener.Stop();
            heartBeatSender.Stop();
            clusterHealthMonitor.Stop();
        }
    }
}