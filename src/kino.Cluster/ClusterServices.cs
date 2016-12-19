namespace kino.Cluster
{
    public class ClusterServices : IClusterServices
    {
        private readonly IClusterMonitor clusterMonitor;
        private readonly IScaleOutListener scaleOutListener;
        private readonly IHeartBeatSender heartBeatSender;
        private readonly IClusterHealthMonitor clusterHealthMonitor;

        public ClusterServices(IClusterMonitor clusterMonitor,
                               IScaleOutListener scaleOutListener,
                               IHeartBeatSender heartBeatSender,
                               IClusterHealthMonitor clusterHealthMonitor)
        {
            this.clusterMonitor = clusterMonitor;
            this.scaleOutListener = scaleOutListener;
            this.heartBeatSender = heartBeatSender;
            this.clusterHealthMonitor = clusterHealthMonitor;
        }

        public IClusterMonitor GetClusterMonitor()
            => clusterMonitor;

        public IClusterHealthMonitor GetClusterHealthMonitor()
            => clusterHealthMonitor;

        public void StopClusterServices()
        {
            clusterMonitor.Stop();
            heartBeatSender.Stop();
            clusterHealthMonitor.Stop();
            scaleOutListener.Stop();
        }

        public void StartClusterServices()
        {
            //NOTE: Should be started first to set SetActiveScaleOutAddress() used by other services
            scaleOutListener.Start();

            clusterMonitor.Start();
            heartBeatSender.Start();
            clusterHealthMonitor.Start();
        }
    }
}