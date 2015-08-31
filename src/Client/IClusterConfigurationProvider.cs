using kino.Connectivity;

namespace Client
{
    public interface IClusterConfigurationProvider
    {
        IClusterConfiguration GetConfiguration();
    }
}