using System;

namespace rawf.Connectivity
{
    public class NodeConfiguration : INodeConfiguration
    {
        public NodeConfiguration(string routerAddress, string localScaleOutAddress)
        {
            RouterAddress = new Uri(routerAddress);
            ScaleOutAddress = new Uri(localScaleOutAddress);
        }

        public Uri RouterAddress { get; }
        public Uri ScaleOutAddress { get; }
    }
}