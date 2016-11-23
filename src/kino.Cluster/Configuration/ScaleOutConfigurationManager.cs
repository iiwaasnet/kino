using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using kino.Core;
using kino.Core.Framework;

namespace kino.Cluster.Configuration
{
    public class ScaleOutConfigurationManager : IScaleOutConfigurationManager
    {
        private readonly ScaleOutSocketConfiguration scaleOutConfig;
        private readonly TaskCompletionSource<SocketEndpoint> scaleOutAddressSource;
        private SocketEndpoint scaleOutAddress;

        public ScaleOutConfigurationManager(ScaleOutSocketConfiguration scaleOutConfig)
        {
            scaleOutAddressSource = new TaskCompletionSource<SocketEndpoint>();
            this.scaleOutConfig = scaleOutConfig;
        }

        public int GetScaleOutReceiveMessageQueueLength()
            => scaleOutConfig.ScaleOutReceiveMessageQueueLength;

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
    }
}