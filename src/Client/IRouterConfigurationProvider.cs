using rawf.Connectivity;

namespace Client
{
    public interface IRouterConfigurationProvider
    {
        IRouterConfiguration GetConfiguration();
    }
}