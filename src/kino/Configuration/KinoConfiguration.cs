using System.Collections.Generic;
using kino.Cluster.Configuration;
using kino.Connectivity;

namespace kino.Configuration
{
    public class KinoConfiguration
    {
        public string ScaleOutAddressUri { get; set; }

        public int ScaleOutReceiveMessageQueueLength { get; set; }

        public IEnumerable<RendezvousNode> RendezvousServers { get; set; }

        public HealthConfiguration Health { get; set; }

        public ClusterMembershipConfiguration Cluster { get; set; }

        public SocketConfiguration Socket { get; set; }
    }
}