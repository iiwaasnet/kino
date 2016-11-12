using System.Collections.Generic;
using kino.Cluster.Configuration;
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
                                   ILocalSendingSocket<IMessage> routerLocalSocket,
                                   IScaleOutConfigurationManager scaleOutConfigurationManager,
                                   ISecurityProvider securityProvider,
                                   IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                   ILogger logger,
                                   IHeartBeatSenderConfigurationManager heartBeatSenderConfigurationManager,
                                   ILocalSocketFactory localSocketFactory,
                                   ClusterHealthMonitorConfiguration clusterHealthMonitorConfiguration)
        {
            if (!membershipConfiguration.RunAsStandalone)
            {
                clusterMonitor = new ClusterMonitor(scaleOutConfigurationProvider,
                                                    autoDiscoverySender,
                                                    autoDiscoveryListener,
                                                    heartBeatSenderConfigurationManager.As<IHeartBeatSenderConfigurationProvider>(),
                                                    routeDiscovery,
                                                    securityProvider);
                scaleOutListener = new ScaleOutListener(socketFactory,
                                                        routerLocalSocket,
                                                        scaleOutConfigurationManager,
                                                        securityProvider,
                                                        performanceCounterManager,
                                                        logger);
                heartBeatSender = new HeartBeatSender(socketFactory,
                                                      heartBeatSenderConfigurationManager,
                                                      scaleOutConfigurationProvider,
                                                      logger);
                clusterHealthMonitor = new ClusterHealthMonitor(socketFactory,
                                                                localSocketFactory,
                                                                securityProvider,
                                                                clusterHealthMonitorConfiguration,
                                                                routerLocalSocket,
                                                                logger);
            }
        }

        public void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain)
            => clusterMonitor?.RegisterSelf(messageHandlers, domain);

        public void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers)
            => clusterMonitor?.UnregisterSelf(messageIdentifiers);

        public void DiscoverMessageRoute(Identifier messageIdentifier)
            => clusterMonitor?.DiscoverMessageRoute(messageIdentifier);

        public void StartPeerMonitoring(Node peer, Health health)
            => clusterHealthMonitor?.StartPeerMonitoring(peer, health);

        public void AddPeer(Node peer, Health health)
            => clusterHealthMonitor?.AddPeer(peer, health);

        public void DeletePeer(SocketIdentifier socketIdentifier)
            => clusterHealthMonitor?.DeletePeer(socketIdentifier);

        public void StartClusterServices()
        {
            clusterMonitor?.Start();
            scaleOutListener?.Start();
            heartBeatSender?.Start();
            clusterHealthMonitor?.Start();
        }

        public void StopClusterServices()
        {
            clusterMonitor?.Stop();
            scaleOutListener?.Stop();
            heartBeatSender?.Stop();
            clusterHealthMonitor?.Stop();
        }
    }
}