namespace kino.Cluster
{
    public interface IClusterServices
    {
        IClusterMonitor GetClusterMonitor();

        IClusterHealthMonitor GetClusterHealthMonitor();

        void StopClusterServices();

        void StartClusterServices();
    }
}