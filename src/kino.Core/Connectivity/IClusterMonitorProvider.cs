namespace kino.Core.Connectivity
{
    public interface IClusterMonitorProvider
    {
        IClusterMonitor GetClusterMonitor();
    }
}