using rawf.Connectivity;

namespace Server
{
    public interface IRouterConfigurationProvider
    {
        IRouterConfiguration GetConfiguration();
    }
}