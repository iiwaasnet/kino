using System.Collections.Generic;

namespace rawf.Actors
{
    public class ConnectivityConfiguration : IConnectivityConfiguration
    {
        private readonly string routerAddress;
        private readonly string localScaleOutAddress;
        private readonly IEnumerable<string> scaleOutCluster;

        public ConnectivityConfiguration(string routerAddress, string localScaleOutAddress, string peerAddress)
        {
            this.routerAddress = routerAddress;
            this.localScaleOutAddress = localScaleOutAddress;
            scaleOutCluster = new[] { peerAddress };
        }

        public string GetRouterAddress()
        {
            return routerAddress;
        }

        public string GetLocalScaleOutAddress()
        {
            return localScaleOutAddress;
        }

        public IEnumerable<string> GetScaleOutCluster()
        {
            return scaleOutCluster;
        }
    }
}