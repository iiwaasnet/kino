using System;
using rawf.Client;
using TypedConfigProvider;

namespace Client
{
    public class MessageHubConfigurationProvider : IMessageHubConfigurationProvider
    {
        private readonly IMessageHubConfiguration config;

        public MessageHubConfigurationProvider(IConfigProvider configProvider)
        {
            var tmp = configProvider.GetConfiguration<ApplicationConfiguration>();
            config = new MessageHubConfiguration {RouterUri = new Uri(tmp.RouterUri)};
        }

        public IMessageHubConfiguration GetConfiguration()
        {
            return config;
        }
    }
}