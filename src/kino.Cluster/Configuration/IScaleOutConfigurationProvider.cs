using kino.Core;

namespace kino.Cluster.Configuration
{
    public interface IScaleOutConfigurationProvider
    {
        RouterConfiguration GetRouterConfiguration();

        SocketEndpoint GetScaleOutAddress();
    }

}