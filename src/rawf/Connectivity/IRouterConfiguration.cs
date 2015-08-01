namespace rawf.Connectivity
{
    public interface IRouterConfiguration
    {
        ClusterMember RouterAddress { get; }
        ClusterMember ScaleOutAddress { get; }
    }
}