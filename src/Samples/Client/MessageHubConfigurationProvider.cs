using System;
using kino.Client;
using TypedConfigProvider;

namespace Client
{
    public class MessageHubConfigurationProvider : IMessageHubConfigurationProvider
    {
        private readonly IMessageHubConfiguration config;

        public MessageHubConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new MessageHubConfiguration {RouterUri = new Uri(appConfig.RouterUri)};
        }

        public IMessageHubConfiguration GetConfiguration()
        {
            return config;
        }
    }
}