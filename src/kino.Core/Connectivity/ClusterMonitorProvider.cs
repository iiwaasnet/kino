namespace kino.Core.Connectivity
{
    public class ClusterMonitorProvider : IClusterMonitorProvider
    {
        private readonly IClusterMonitor clusterMonitor;

        public ClusterMonitorProvider(ClusterMembershipConfiguration membershipConfiguration,
                                      RouterConfiguration routerConfiguration,
                                      IClusterMembership clusterMembership,
                                      IClusterMessageSender clusterMessageSender,
                                      IClusterMessageListener clusterMessageListener)
        {
            clusterMonitor = membershipConfiguration.RunAsStandalone
                                 ? (IClusterMonitor) new LoopbackClusterMonitor()
                                 : new ClusterMonitor(routerConfiguration,
                                                      clusterMembership,
                                                      clusterMessageSender,
                                                      clusterMessageListener);
        }

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;
    }
}