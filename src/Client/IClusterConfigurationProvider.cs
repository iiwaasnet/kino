using rawf.Connectivity;

namespace Client
{
    public interface IClusterConfigurationProvider
    {
        IClusterConfiguration GetConfiguration();
    }
}