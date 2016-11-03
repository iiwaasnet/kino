using kino.Core;

namespace kino.Configuration
{
    public interface IScaleOutConfigurationProvider
    {
        RouterConfiguration GetRouterConfiguration();

        SocketEndpoint GetScaleOutAddress();
    }

}