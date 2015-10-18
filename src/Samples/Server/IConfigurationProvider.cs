using System.Collections.Generic;
using kino.Connectivity;

namespace Server
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();
        RouterConfiguration GetRouterConfiguration();
        ClusterMembershipConfiguration GetClusterTimingConfiguration();
    }
}