using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace kino.Core.Connectivity
{
    public class RouterConfigurationManager : IRouterConfigurationManager
    {
        private readonly ScaleOutSocketConfiguration scaleOutConfig;
        private readonly TaskCompletionSource<SocketEndpoint> scaleOutAddressSource;
        private SocketEndpoint scaleOutAddress;
        private readonly TaskCompletionSource<RouterConfiguration> activeRouterConfigSource;
        private RouterConfiguration activeRouterConfig;
        private RouterConfiguration routerConfig;

        public RouterConfigurationManager(RouterConfiguration routerConfig,
                                          ScaleOutSocketConfiguration scaleOutConfig)
        {
            scaleOutAddressSource = new TaskCompletionSource<SocketEndpoint>();
            activeRouterConfigSource = new TaskCompletionSource<RouterConfiguration>();
            this.scaleOutConfig = scaleOutConfig;
            this.routerConfig = SetDefaultsForMissingMembers(routerConfig);
        }

        public RouterConfiguration GetRouterConfiguration()
            => routerConfig ?? (routerConfig = activeRouterConfigSource.Task.Result);

        public SocketEndpoint GetScaleOutAddress()
            => scaleOutAddress ?? (scaleOutAddress = scaleOutAddressSource.Task.Result);

        public IEnumerable<SocketEndpoint> GetScaleOutAddressRange()
            => scaleOutConfig.AddressRange;

        public void SetActiveScaleOutAddress(SocketEndpoint activeAddress)
            => scaleOutAddressSource.SetResult(activeAddress);

        public RouterConfiguration GetInactiveRouterConfiguration()
            => routerConfig;

        public void SetMessageRouterConfigurationActive()
            => activeRouterConfigSource.SetResult(routerConfig);

        private RouterConfiguration SetDefaultsForMissingMembers(RouterConfiguration routerConfiguration)
        {
            routerConfiguration.ConnectionEstablishWaitTime = routerConfiguration.ConnectionEstablishWaitTime <= TimeSpan.Zero
                                                                  ? TimeSpan.FromMilliseconds(200)
                                                                  : routerConfiguration.ConnectionEstablishWaitTime;
            return routerConfiguration;
        }
    }
}