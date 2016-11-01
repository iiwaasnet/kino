namespace kino.Cluster
{
    public interface IClusterMonitorProvider
    {
        IClusterMonitor GetClusterMonitor();
    }
}