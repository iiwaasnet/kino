namespace kino.Cluster
{
    public interface IClusterConnectivity
    {
        IClusterMonitor GetClusterMonitor();

        IScaleOutListener GetScaleOutListener();

        IHeartBeatSender GetHeartBeatSender();

        IClusterHealthMonitor GetClusterHealthMonitor();
    }
}