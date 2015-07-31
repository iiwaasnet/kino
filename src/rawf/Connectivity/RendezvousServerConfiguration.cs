using System;

namespace rawf.Connectivity
{
    public class RendezvousServerConfiguration
    {
        public Uri BroadcastEndpoint { get; set; }
        public ClusterMember UnicastEndpoint { get; set; }
    }
}