using rawf.Client;

namespace Client
{
    public interface IMessageHubConfigurationProvider
    {
        IMessageHubConfiguration GetConfiguration();
    }
}