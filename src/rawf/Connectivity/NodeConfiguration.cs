using System;

namespace rawf.Connectivity
{
    public class NodeConfiguration : INodeConfiguration
    {
        private static readonly byte[] RouterIdentity = {0, 1, 2, 3};
        private static readonly byte[] ScaleOutSocketIdentity = {4, 5, 6, 7};

        public NodeConfiguration(string routerAddress, string localScaleOutAddress)
        {
            RouterAddress = new ClusterMember
                            {
                                Uri = new Uri(routerAddress),
                                Identity = RouterIdentity
                            };
            ScaleOutAddress = new ClusterMember
                              {
                                  Uri = new Uri(localScaleOutAddress),
                                  Identity = ScaleOutSocketIdentity
                              };
        }

        public ClusterMember RouterAddress { get; }
        public ClusterMember ScaleOutAddress { get; }
    }
}