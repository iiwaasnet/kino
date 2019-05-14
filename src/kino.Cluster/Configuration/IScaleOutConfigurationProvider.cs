using kino.Core;

namespace kino.Cluster.Configuration
{
    public interface IScaleOutConfigurationProvider
    {
        int GetScaleOutReceiveMessageQueueLength();

        SocketEndpoint GetScaleOutAddress();
    }
}