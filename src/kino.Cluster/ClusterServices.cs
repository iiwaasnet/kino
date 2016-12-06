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
            scaleOutListener.Stop();
            heartBeatSender.Stop();
            clusterHealthMonitor.Stop();
        }

        public void StartClusterServices()
        {
            clusterMonitor.Start();
            scaleOutListener.Start();
            heartBeatSender.Start();
            clusterHealthMonitor.Start();
        }
    }
}