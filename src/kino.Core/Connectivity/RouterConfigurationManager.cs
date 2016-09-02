using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace kino.Core.Connectivity
{
    public class RouterConfigurationManager : IRouterConfigurationManager
    {
        private readonly ScaleOutSocketConfiguration scaleOutConfig;
        private readonly TaskCompletionSource<SocketEndpoint> scaleOutAddress;
        private readonly TaskCompletionSource<RouterConfiguration> activeRouterConfig;
        private readonly RouterConfiguration routerConfig;

        public RouterConfigurationManager(RouterConfiguration routerConfig,
                                          ScaleOutSocketConfiguration scaleOutConfig)
        {
            scaleOutAddress = new TaskCompletionSource<SocketEndpoint>();
            activeRouterConfig = new TaskCompletionSource<RouterConfiguration>();
            this.scaleOutConfig = scaleOutConfig;
            this.routerConfig = SetDefaultsForMissingMembers(routerConfig);
        }

        public Task<RouterConfiguration> GetRouterConfiguration()
            => activeRouterConfig.Task;

        public Task<SocketEndpoint> GetScaleOutAddress()
            => scaleOutAddress.Task;

        public IEnumerable<SocketEndpoint> GetScaleOutAddressRange()
            => scaleOutConfig.AddressRange;

        public void SetActiveScaleOutAddress(SocketEndpoint activeAddress)
            => scaleOutAddress.SetResult(activeAddress);

        public RouterConfiguration GetInactiveRouterConfiguration()
            => routerConfig;

        public void SetMessageRouterConfigurationActive()
            => activeRouterConfig.SetResult(routerConfig);

        private RouterConfiguration SetDefaultsForMissingMembers(RouterConfiguration routerConfiguration)
        {
            routerConfiguration.ConnectionEstablishWaitTime = routerConfiguration.ConnectionEstablishWaitTime <= TimeSpan.Zero
                                                                  ? TimeSpan.FromMilliseconds(200)
                                                                  : routerConfiguration.ConnectionEstablishWaitTime;
            return routerConfiguration;
        }
    }
}