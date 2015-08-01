using System;

namespace rawf.Connectivity
{
    public class RouterConfiguration : IRouterConfiguration
    {
        public ClusterMember RouterAddress { get; set; }
        public ClusterMember ScaleOutAddress { get; set; }
    }
}