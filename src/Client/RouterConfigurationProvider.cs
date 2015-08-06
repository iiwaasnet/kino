using System;
using rawf.Connectivity;
using TypedConfigProvider;

namespace Client
{
    public class RouterConfigurationProvider : IRouterConfigurationProvider
    {
        private readonly IRouterConfiguration config;

        public RouterConfigurationProvider(IConfigProvider configProvider)
        {
            var tmp = configProvider.GetConfiguration<ApplicationConfiguration>();

            config = new RouterConfiguration
                     {
                         RouterAddress = new SocketEndpoint(new Uri(tmp.RouterUri), SocketIdentifier.CreateNew()),
                         ScaleOutAddress = new SocketEndpoint(new Uri(tmp.ScaleOutAddressUri), SocketIdentifier.CreateNew())
                     };
        }

        public IRouterConfiguration GetConfiguration()
            => config;
    }
}