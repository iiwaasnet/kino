using kino.Connectivity;

namespace Client
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetConfiguration();
    }
}