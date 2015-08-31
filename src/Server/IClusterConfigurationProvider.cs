using kino.Connectivity;

namespace Server
{
    public interface IClusterConfigurationProvider
    {
        IClusterConfiguration GetConfiguration();
    }
}