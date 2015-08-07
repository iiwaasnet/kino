using rawf.Connectivity;

namespace Server
{
    public interface IClusterConfigurationProvider
    {
        IClusterConfiguration GetConfiguration();
    }
}