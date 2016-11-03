using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Core;
using kino.Core.Framework;

namespace kino.Configuration
{
    public class ScaleOutConfigurationManager : IScaleOutConfigurationManager
    {
        private readonly RouterConfiguration routerConfig;
        private readonly ScaleOutSocketConfiguration scaleOutConfig;
        private readonly TaskCompletionSource<SocketEndpoint> scaleOutAddressSource;
        private SocketEndpoint scaleOutAddress;
        private readonly TaskCompletionSource<RouterConfiguration> activeRouterConfigSource;
        private RouterConfiguration activeRouterConfig;
        private readonly RouterConfiguration inactiveRouterConfig;

        public ScaleOutConfigurationManager(RouterConfiguration routerConfig,
                                          ScaleOutSocketConfiguration scaleOutConfig)
        {
            scaleOutAddressSource = new TaskCompletionSource<SocketEndpoint>();
            this.scaleOutConfig = scaleOutConfig;
            this.routerConfig = SetDefaultsForMissingMembers(routerConfig);
        }

        public RouterConfiguration GetRouterConfiguration()
            => routerConfig;

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

        private static RouterConfiguration SetDefaultsForMissingMembers(RouterConfiguration routerConfiguration)
        {
            routerConfiguration.ConnectionEstablishWaitTime = routerConfiguration.ConnectionEstablishWaitTime <= TimeSpan.Zero
                                                                  ? TimeSpan.FromMilliseconds(200)
                                                                  : routerConfiguration.ConnectionEstablishWaitTime;
            return routerConfiguration;
        }
    }
}