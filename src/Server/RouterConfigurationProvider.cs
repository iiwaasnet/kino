using System;
using rawf.Connectivity;
using TypedConfigProvider;

namespace Server
{
    public class RouterConfigurationProvider : IRouterConfigurationProvider
    {
        private readonly IRouterConfiguration config;

        public RouterConfigurationProvider(IConfigProvider configProvider)
        {
            var tmp = configProvider.GetConfiguration<ApplicationConfiguration>();

            config = new RouterConfiguration
                     {
                         RouterAddress = new SocketEndpoint(new Uri(tmp.RouterUri), tmp.RouterSocketIdentity),
                         ScaleOutAddress = new SocketEndpoint(new Uri(tmp.ScaleOutAddressUri), tmp.ScaleOutSocketIdentity)
                     };
        }

        public IRouterConfiguration GetConfiguration()
            => config;
    }
}