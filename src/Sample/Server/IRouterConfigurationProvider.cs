using kino.Connectivity;

namespace Server
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetConfiguration();
    }
}