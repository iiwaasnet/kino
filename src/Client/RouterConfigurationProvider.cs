using System;
using rawf.Connectivity;

namespace Client
{
    public class RouterConfigurationProvider
    {
        private readonly RouterConfiguration config;

        public RouterConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new RouterConfiguration
                     {
                         RouterAddress = new SocketEndpoint(new Uri(appConfig.RouterUri), SocketIdentifier.CreateNew()),
                         ScaleOutAddress = new SocketEndpoint(new Uri(appConfig.ScaleOutAddressUri), SocketIdentifier.CreateNew())
                     };
        }

        public RouterConfiguration GetConfiguration()
            => config;
    }
}