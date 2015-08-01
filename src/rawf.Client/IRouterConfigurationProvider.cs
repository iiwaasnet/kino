using rawf.Connectivity;

namespace rawf.Client
{
    public interface IRouterConfigurationProvider
    {
        IRouterConfiguration GetConfiguration();
    }
}