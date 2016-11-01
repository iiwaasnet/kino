using kino.Core;

namespace kino.Configuration
{
    public interface IRouterConfigurationProvider
    {
        RouterConfiguration GetRouterConfiguration();

        SocketEndpoint GetScaleOutAddress();
    }
}