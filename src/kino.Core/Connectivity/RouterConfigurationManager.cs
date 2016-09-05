using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Core.Framework;

namespace kino.Core.Connectivity
{
    public class RouterConfigurationManager : IRouterConfigurationManager
    {
        private readonly ScaleOutSocketConfiguration scaleOutConfig;
        private readonly TaskCompletionSource<SocketEndpoint> scaleOutAddressSource;
        private SocketEndpoint scaleOutAddress;
        private readonly TaskCompletionSource<RouterConfiguration> activeRouterConfigSource;
        private RouterConfiguration activeRouterConfig;
        private readonly RouterConfiguration inactiveRouterConfig;

        public RouterConfigurationManager(RouterConfiguration routerConfig,
                                          ScaleOutSocketConfiguration scaleOutConfig)
        {
            scaleOutAddressSource = new TaskCompletionSource<SocketEndpoint>();
            activeRouterConfigSource = new TaskCompletionSource<RouterConfiguration>();
            this.scaleOutConfig = scaleOutConfig;
            inactiveRouterConfig = SetDefaultsForMissingMembers(routerConfig);
        }

        public RouterConfiguration GetRouterConfiguration()
            => activeRouterConfig ?? (activeRouterConfig = activeRouterConfigSource.Task.Result);

        public SocketEndpoint GetScaleOutAddress()
            => scaleOutAddress ?? (scaleOutAddress = scaleOutAddressSource.Task.Result);

        public IEnumerable<SocketEndpoint> GetScaleOutAddressRange()
            => scaleOutConfig.AddressRange;

        public void SetActiveScaleOutAddress(SocketEndpoint activeAddress)
        {
            if (scaleOutConfig.AddressRange.Contains(activeAddress))
            {
                scaleOutAddressSource.SetResult(activeAddress);
            }
            else
            {
                throw new Exception($"SocketEndpoint {activeAddress.Uri.ToSocketAddress()} is not configured!");
            }
        }
            

        public RouterConfiguration GetInactiveRouterConfiguration()
            => inactiveRouterConfig;

        public void SetMessageRouterConfigurationActive()
            => activeRouterConfigSource.SetResult(inactiveRouterConfig);

        private static RouterConfiguration SetDefaultsForMissingMembers(RouterConfiguration routerConfiguration)
        {
            routerConfiguration.ConnectionEstablishWaitTime = routerConfiguration.ConnectionEstablishWaitTime <= TimeSpan.Zero
                                                                  ? TimeSpan.FromMilliseconds(200)
                                                                  : routerConfiguration.ConnectionEstablishWaitTime;
            return routerConfiguration;
        }
    }
}