using System.Collections.Generic;
using kino.Core.Connectivity;

namespace Server
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();
        RouterConfiguration GetRouterConfiguration();
        ClusterMembershipConfiguration GetClusterTimingConfiguration();
    }
}