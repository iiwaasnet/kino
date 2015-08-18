using rawf.Connectivity;

namespace Client
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetConfiguration();
    }
}