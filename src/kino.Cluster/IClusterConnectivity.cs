namespace kino.Cluster
{
    public interface IClusterConnectivity
    {
        IClusterMonitor GetClusterMonitor();

        IScaleOutListener GetScaleOutListener();
    }
}