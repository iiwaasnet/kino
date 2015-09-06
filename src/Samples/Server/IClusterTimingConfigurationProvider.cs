using kino.Connectivity;

namespace Server
{
    public interface IClusterTimingConfigurationProvider
    {
        ClusterTimingConfiguration GetConfiguration();
    }
}