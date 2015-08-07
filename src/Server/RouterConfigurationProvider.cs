using System;
using rawf.Connectivity;
using TypedConfigProvider;

namespace Server
{
    public class RouterConfigurationProvider : IRouterConfigurationProvider
    {
        private readonly IRouterConfiguration config;

        public RouterConfigurationProvider(ApplicationConfiguration appConfig)
        {

            config = new RouterConfiguration
                     {
                         RouterAddress = new SocketEndpoint(new Uri(appConfig.RouterUri), SocketIdentifier.CreateNew()),
                         ScaleOutAddress = new SocketEndpoint(new Uri(appConfig.ScaleOutAddressUri), SocketIdentifier.CreateNew())
                     };
        }

        public IRouterConfiguration GetConfiguration()
            => config;
    }
}