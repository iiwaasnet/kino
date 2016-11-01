using kino.Core.Connectivity;

namespace kino.Configuration
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetRouterConfiguration();

        SocketEndpoint GetScaleOutAddress();
    }
}