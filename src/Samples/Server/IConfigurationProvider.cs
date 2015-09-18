using System.Collections.Generic;
using kino.Connectivity;

namespace Server
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoints> GetRendezvousEndpointsConfiguration();
        RouterConfiguration GetRouterConfiguration();
        ClusterMembershipConfiguration GetClusterTimingConfiguration();
    }
}