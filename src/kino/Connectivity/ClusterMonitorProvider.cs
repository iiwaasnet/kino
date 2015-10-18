using kino.Diagnostics;
using kino.Sockets;

namespace kino.Connectivity
{
    public class ClusterMonitorProvider : IClusterMonitorProvider
    {
        private readonly IClusterMonitor clusterMonitor;

        public ClusterMonitorProvider(ISocketFactory socketFactory,
                                      RouterConfiguration routerConfiguration,
                                      IClusterMembership clusterMembership,
                                      ClusterMembershipConfiguration membershipConfiguration,
                                      IRendezvousCluster rendezvousCluster,
                                      ILogger logger)
        {
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) new LoopbackClusterMonitor()
                                 : new ClusterMonitor(socketFactory,
                                                      routerConfiguration,
                                                      clusterMembership,
                                                      membershipConfiguration,
                                                      rendezvousCluster,
                                                      logger);
        }

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;
    }
}