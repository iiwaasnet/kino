using kino.Connectivity;

namespace Client
{
    public interface IClusterTimingConfigurationProvider
    {
        ClusterTimingConfiguration GetConfiguration();
    }
}