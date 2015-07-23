namespace rawf.Connectivity
{
    public interface IClusterConfigurationMonitor
    {
        void RegisterMember(ClusterMember member);
        void UnregisterMember(ClusterMember member);
    }
}